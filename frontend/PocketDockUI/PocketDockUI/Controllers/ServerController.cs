using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc;
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
public class ServerController : ControllerBase
{
    private readonly PocketDockContext _context;
    private readonly ServerConfig _serverConfig;
    private readonly IProxyConfigProvider _proxyConfigProvider;
    private readonly IMemoryCache _memoryCache;
    private readonly ProcessManager _processManager;

    public ServerController(PocketDockContext context, IOptions<ServerConfig> serverConfig, IProxyConfigProvider proxyConfigProvider, IMemoryCache memoryCache, ProcessManager processManager)
    {
        _context = context;
        _serverConfig = serverConfig.Value;
        _proxyConfigProvider = proxyConfigProvider;
        _memoryCache = memoryCache;
        _processManager = processManager;
    }
    [HttpGet("Settings")]
    public async Task<ActionResult<string>> GetSettings([Required]string serverId, [Required]string remoteServerPass)
    {
        if (_serverConfig.ApiKey != remoteServerPass)
        {
            return Unauthorized("Invalid api key");
        }
        
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
    public async Task<ActionResult> RemoveServer([Required] string serverId, [Required] string remoteServerPass)
    {
        if (_serverConfig.ApiKey != remoteServerPass)
        {
            return Unauthorized("Invalid api key");
        }

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

        if (_serverConfig.ProxyEnabled)
        {
            _processManager.StopProcess(server);
        }

        _context.DeallocateServer(server);
        
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(10));

        await _context.SaveChangesAsync();
        
        _memoryCache.Set(CacheKeys.ExpiredAssignedPrefix + assignedUserId, true, cacheEntryOptions);

        return Ok();
    }

    [HttpPost("UpdateStats")]
    public async Task<ActionResult> UpdateStats(
        [FromQuery][Required] string serverId,
        [FromQuery][Required] string remoteServerPass,
        [FromQuery][Required] bool hasPlayers)
    {
        if (_serverConfig.ApiKey != remoteServerPass)
        {
            return Unauthorized("Invalid api key");
        }

        var server = await _context.Server.FirstOrDefaultAsync(x => x.ServerId == serverId);
        if (server == null)
        {
            return NotFound("Server not found");
        }
        
        server.ServerAssignment.StartTime ??= DateTimeOffset.Now.ToUniversalTime();
        server.ServerAssignment.LastActivity = hasPlayers ? null : DateTimeOffset.Now.ToUniversalTime();

        await _context.SaveChangesAsync();

        return Ok();
    }

    //Call this if DB is updated manually
    [HttpGet("ReloadCache")]
    public async Task<ActionResult> ReloadCache([FromQuery] [Required] string remoteServerPass)
    {
        if (_serverConfig.ApiKey != remoteServerPass)
        {
            return Unauthorized("Invalid api key");
        }
        ((DbConfigProvider)_proxyConfigProvider).Update();
        return Ok("Done");
    }

    [HttpPost("UpdateServerList")]
    public async Task<ActionResult> UpdateServerList([FromForm] ServerUpdateDetails serverInfo, [FromQuery] [Required] string remoteServerPass)
    {
        if (_serverConfig.ApiKey != remoteServerPass)
        {
            return Unauthorized("Invalid api key");
        }
        using var reader = new StreamReader(serverInfo.Machines.OpenReadStream());
        var machinesBody = await reader.ReadToEndAsync();

        var servers = await _context.Server.ToListAsync();
        var machines = JsonConvert.DeserializeObject<List<FlyMachine>>(machinesBody);
        foreach (var machine in machines)
        {
            var server = servers.FirstOrDefault(x => x.ServerId == machine.Id);
            if (server == null)
            {
                server = new Server();
                _context.Server.Add(server);
            }
            
            var triggerPort = machine.Config.Services
                .First(x => x.InternalPort == 7000 && x.Protocol == "tcp").Ports.First().Port;

            server.PrivateIpAddress = $"[{machine.PrivateIp}]";
            server.TriggerIpAddress = $"[{serverInfo.TriggerIp}]";
            server.Domain = serverInfo.Domain;
            server.ServerId = machine.Id;
            server.TriggerPort = triggerPort;
            server.GameServerPortRangeStart = triggerPort + 2000;
            server.GameServerPortRangeEnd = triggerPort + 2099;
        }

        //Remove all servers that aren't in serverInfo.Machines
        var serverIds = machines.Select(x => x.Id).ToList();
        var serversToRemove = servers.Where(x => !serverIds.Contains(x.ServerId)).ToList();
        _context.Server.RemoveRange(serversToRemove);

        await _context.SaveChangesAsync();
        
        ((DbConfigProvider)_proxyConfigProvider).Update();
        
        return Ok();
    }
}