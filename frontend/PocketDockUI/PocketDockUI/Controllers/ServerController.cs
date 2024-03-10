using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PocketDockUI.Data;
using PocketDockUI.Data.Models;
using PocketDockUI.Models;
using PocketDockUI.Services;
using Yarp.ReverseProxy.Configuration;

namespace PocketDockUI.Controllers;

[Route("api/Server")]
[ApiController]
public class ServerController : ControllerBase, IActionFilter
{
    private readonly PocketDockContext _context;
    private readonly ServerConfig _serverConfig;
    private readonly IProxyConfigProvider _proxyConfigProvider;
    private readonly IMemoryCache _memoryCache;
    private readonly FlyService _flyService;

    public ServerController(PocketDockContext context,
        IOptions<ServerConfig> serverConfig,
        IProxyConfigProvider proxyConfigProvider,
        IMemoryCache memoryCache,
        FlyService flyService)
    {
        _context = context;
        _serverConfig = serverConfig.Value;
        _proxyConfigProvider = proxyConfigProvider;
        _memoryCache = memoryCache;
        _flyService = flyService;
    }
    
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var hasServerPass = Request.Query.TryGetValue("remoteServerPass", out var remoteServerPass);
        if (!hasServerPass)
        {
            context.Result = BadRequest("No api key provided");
            return;
        }
        if (_serverConfig.ApiKey != remoteServerPass.First())
        {
            context.Result = Unauthorized("Invalid api key");
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
    
    [HttpGet("Settings")]
    public async Task<ActionResult<string>> GetSettings([Required]string serverId)
    {
        var server = await _context.Server.FirstOrDefaultAsync(x => x.ServerId == serverId);
        if (server == null)
        {
            return NotFound("Server not found");
        }

        if (server.ServerAssignment == null)
        {
            return BadRequest("Server is not assigned");
        }
        
        var values = new Dictionary<string, string>()
        {
            ["LOCAL_SERVER_PASS"] = server.ServerAssignment.ServerPass,
            ["PMPORT"] = server.ServerAssignment.GameServerPort.ToString(),
            ["SERVER_TIMEOUT"] = _serverConfig.ServerTimeout.ToString(),
            ["INACTIVITY_TIMEOUT"] = _serverConfig.InactivityTimeout.ToString(),
            ["PM_VERSION"] = server.ServerAssignment.SelectedVersion ?? ""
        };
        var data = string.Join("\n", values.Select(kv => $"{kv.Key}='{kv.Value}'"));
        return Content(data);
    }

    [HttpPost("RemoveServer")]
    public async Task<ActionResult> RemoveServer([Required] string serverId)
    {
        var server = await _context.Server
            .Include(x => x.ServerAssignment)
            .FirstOrDefaultAsync(x => x.ServerId == serverId);
        if (server == null)
        {
            return NotFound("Server not found");
        }
        
        if (server.ServerAssignment == null)
        {
            return BadRequest("Server is not assigned");
        }

        var assignedUserId = server.ServerAssignment.AssignedUserId;

        _context.DeallocateServer(server);
        
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(10));

        await _context.SaveChangesAsync();
        
        _memoryCache.Set(CacheKeys.ExpiredAssignedPrefix + assignedUserId, true, cacheEntryOptions);

        return Ok();
    }
    
    [HttpPost("DeallocateIp")]
    public async Task<ActionResult> DeallocateIp([Required] string proxyId)
    {
        var proxy = await _context.Proxy.FirstOrDefaultAsync(x => x.ServerId == proxyId);
        if (proxy == null)
        {
            return NotFound("Server not found");
        }

        await _flyService.ReleaseIpAddress(proxy.AppName, proxy.IpAddress);
        proxy.IpAddress = null;

        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("UpdateStats")]
    public async Task<ActionResult> UpdateStats(
        [FromQuery][Required] string serverId,
        [FromQuery][Required] bool hasPlayers)
    {
        var server = await _context.Server.FirstOrDefaultAsync(x => x.ServerId == serverId);
        if (server == null)
        {
            return NotFound("Server not found");
        }
        
        server.ServerAssignment.StartTime ??= DateTimeOffset.Now.ToUniversalTime();
        server.ServerAssignment.LastActivity = hasPlayers ? null : DateTimeOffset.Now.ToUniversalTime();

        await _context.SaveChangesAsync();

        if (server.ServerAssignment.Proxy != null)
        {
            //Keep proxy alive
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            await httpClient.GetAsync($"http://{server.ServerAssignment.Proxy.IpAddress}:4894");   
        }

        return Ok();
    }

    //Call this if DB is updated manually
    [HttpGet("ReloadCache")]
    public async Task<ActionResult> ReloadCache()
    {
        ((DbConfigProvider)_proxyConfigProvider).Update();
        return Ok("Done");
    }

    [HttpPost("UpdateServerList")]
    public async Task<ActionResult> UpdateServerList([FromForm] ServerUpdateDetails serverInfo)
    {
        using var reader = new StreamReader(serverInfo.Machines.OpenReadStream());
        var machinesBody = await reader.ReadToEndAsync();

        var servers = await _context.Server.ToListAsync();
        var allMachines = JsonConvert.DeserializeObject<List<FlyMachine>>(machinesBody);
        var proxies = await _context.Proxy.ToListAsync();

        foreach (var proxy in proxies)
        {
            var machines = allMachines.Where(x => x.Region == proxy.Region).ToList();
            var startingPort = 2000;
            var portCount = 49;
            if (!machines.Any())
            {
                for (var i = 0; i < serverInfo.CountPerRegion; i++)
                {
                    var server = new Server
                    {
                        GameServerPortRangeStart = startingPort,
                        GameServerPortRangeEnd = startingPort + portCount,
                        Region = proxy.Region,
                        IsTemporaryServer = true
                    };
                    _context.Server.Add(server);
                    startingPort += portCount + 1;
                }
                continue;
            }
            foreach (var machine in machines)
            {
                var server = servers.FirstOrDefault(x => x.ServerId == machine.Id);
                if (server == null)
                {
                    server = new Server();
                    _context.Server.Add(server);
                }

                server.PrivateIpAddress = $"[{machine.PrivateIp}]";
                server.ServerId = machine.Id;
                server.GameServerPortRangeStart = startingPort;
                server.GameServerPortRangeEnd = startingPort + portCount;
                server.Region = machine.Region;
            
                startingPort += portCount + 1;
            }
        }

        //Remove all servers that aren't in serverInfo.Machines
        var serverIds = allMachines.Select(x => x.Id).ToList();
        var serversToRemove = servers.Where(x => !serverIds.Contains(x.ServerId) && !x.IsTemporaryServer).ToList();
        _context.Server.RemoveRange(serversToRemove);

        await _context.SaveChangesAsync();
        
        ((DbConfigProvider)_proxyConfigProvider).Update();
        
        return Ok();
    }

    [HttpPost("UpdateProxyList")]
    public async Task<ActionResult> UpdateProxyList()
    {
        var proxyMachines = await _flyService.GetMachineInfoForProxies();
        proxyMachines = proxyMachines
            .Where(x => x.AppName.StartsWith(_serverConfig.FlyProxyAppNamePrefix + "-proxy", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        var proxies = await _context.Proxy.ToListAsync();

        var jsonText = await System.IO.File.ReadAllTextAsync("Services/FlyRegions.json");
        var flyRegionInfo = JsonConvert.DeserializeObject<List<FlyRegionInfo>>(jsonText);

        foreach (var proxyMachine in proxyMachines)
        {
            var proxy = proxies.FirstOrDefault(x => x.Region == proxyMachine.Region);
            var regionInfo = flyRegionInfo.FirstOrDefault(x => x.Code == proxyMachine.Region);
            if (proxy == null)
            {
                proxy = new Proxy();
                _context.Proxy.Add(proxy);
            }
            proxy.IpAddress = proxyMachine.IpAddress;
            proxy.Region = proxyMachine.Region;
            proxy.ServerId = proxyMachine.MachineId;
            proxy.AppName = proxyMachine.AppName;
            proxy.DisplayName = regionInfo?.Name ?? proxyMachine.Region;
            proxy.Latitude = regionInfo?.Latitude;
            proxy.Longitude = regionInfo?.Longitude;
            
        }
        
        var proxyIds = proxyMachines.Select(x => x.Region).ToList();
        var proxiesToRemove = proxies.Where(x => !proxyIds.Contains(x.Region)).ToList();
        _context.Proxy.RemoveRange(proxiesToRemove);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("TraefikProxyConfig")]
    public async Task<ActionResult> GetTraefikProxyConfig([Required] [BindRequired] string region)
    {
        var regionServers = await _context.Server
            .Include(x => x.ServerAssignment)
            .Where(x => x.ServerAssignment != null)
            .Where(x => x.Region == region)
            .ToListAsync();

        return Ok(regionServers.Any()
            ? TraefikConfigGenerator.Get(regionServers)
            : new {});
    }
}

public static class TraefikConfigGenerator
{
    public static object Get(List<Server> servers)
    {
        var traefikUdp = new 
        {
            Routers = new Dictionary<string, object>(),
            Services = new Dictionary<string, object>()
        };

        foreach (var server in servers)
        {
            var port = server.ServerAssignment.GameServerPort;
            var hostName = server.PrivateIpAddress;
            
            string routerName = "router-" + port;
            string entryPointName = "entrypoint-" + port;
            string serviceName = "service-" + port;

            traefikUdp.Routers[routerName] = new
            {
                EntryPoints = new List<string> { entryPointName },
                Service = serviceName
            };

            traefikUdp.Services[serviceName] = new
            {
                LoadBalancer = new
                {
                    Servers = new List<object>
                    {
                        new
                        {
                            Address = hostName + ":" + port
                        }
                    }
                }
            };
        }

        return new 
        {
            Udp = traefikUdp
        };
    }
}