using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PocketDockUI.Data;
using PocketDockUI.Data.Models;
using PocketDockUI.Extensions;
using PocketDockUI.Models;
using PocketDockUI.Services;
using PocketDockUI.ViewModels;
using Yarp.ReverseProxy.Configuration;

namespace PocketDockUI.Controllers;

public class HomeController : BaseController
{
    private readonly PocketDockContext _context;
    private readonly RecaptchaService _recaptcha;
    private readonly IMemoryCache _memoryCache;
    private readonly FlyService _flyService;
    private readonly IProxyConfigProvider _proxyConfigProvider;
    private readonly ServerConfig _serverConfig;
    private readonly ILogger<HomeController> _logger;
    private static readonly SemaphoreSlim _regionSemaphore = new(1, 1);
    //Normally not needed, but prevents running multiple DB operations at once while running background tasks
    private readonly SemaphoreSlim _dbContextSemaphore = new(1, 1);

    public HomeController(ILogger<HomeController> logger,
        PocketDockContext context,
        RecaptchaService recaptcha,
        IOptions<ServerConfig> serverConfig,
        IMemoryCache memoryCache,
        FlyService flyService,
        IProxyConfigProvider proxyConfigProvider) : base(logger)
    {
        _logger = logger;
        _context = context;
        _recaptcha = recaptcha;
        _memoryCache = memoryCache;
        _serverConfig = serverConfig.Value;
        _flyService = flyService;
        _proxyConfigProvider = proxyConfigProvider;
    }

    public async Task<IActionResult> Index()
    {
        var cachedVersions = await GetPmVersions();
        
        var hasServer = false;
        var userId = HttpContext.Session.GetKey(SessionKey.UserId);
        
        if (userId != null)
        {
            hasServer = await _context.Server.AnyAsync(x => x.ServerAssignment.AssignedUserId == userId);
            if (!hasServer)
            {
                HttpContext.Session.RemoveKey(SessionKey.UserId);
            }
        }

        var sessionMessage = HttpContext.Session.GetBanner();
        var regions = await _context.Proxy
            .Select(x => new ProxyDto()
            {
                Region = x.Region,
                DisplayName = x.DisplayName,
                Latitude = x.Latitude,
                Longitude = x.Longitude
            })
            .OrderBy(x => x.DisplayName)
            .ToListAsync();
        var viewModel = new IndexPageViewModel(hasServer, sessionMessage, cachedVersions, regions.First().Region, regions);
        return View(viewModel);
    }

    public async Task<IActionResult> Server()
    {
        var sessionId = HttpContext.Session.GetKey(SessionKey.UserId);
        if (sessionId == null)
        {
            return GoHome("noSessionFound2", "You do not currently have a server, please create one below.");

        }
        var server = await _context.Server
            .Include(x => x.ServerAssignment.Proxy)
            .SingleOrDefaultAsync(x => x.ServerAssignment.AssignedUserId == sessionId);
        if (server == null)
        {
            return GoHome("noServerFound2", "There was an error finding your server, please create a new one.");
        }
        return View(new ServerPageViewModel(server));
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> Launch([FromForm] LaunchParameters parameters)
    {
        if (!await _recaptcha.Verify(parameters.RecaptchaToken))
        {
            return GoHome("invalidRecaptcha", "Please fill out the captcha if prompted.");
        }
        
        var selectedVersion = parameters.SelectedVersion != "default" ? parameters.SelectedVersion : null;

        if (selectedVersion != null)
        {
            var cachedVersions = await GetPmVersions();
            if (!cachedVersions.Contains(parameters.SelectedVersion, StringComparer.OrdinalIgnoreCase))
            {
                return GoHome("invalidVersion", "Please select a valid version.");
            }
        }

        var regionProxy = _context.Proxy.SingleOrDefault(x => x.Region == parameters.SelectedRegion);
        if (regionProxy == null)
        {
            return GoHome("invalidRegion", "Please select a valid region.");
        }

        Server currentServer = null;
        try
        {
            var userId = HttpContext.Session.GetKey(SessionKey.UserId);
            var userIp = HttpContext.Connection.RemoteIpAddress.ToString();
            currentServer = await _context.Server.FirstOrDefaultAsync(x => 
                x.ServerAssignment != null
                && (x.ServerAssignment.AssignedUserIpAddress == userIp || x.ServerAssignment.AssignedUserId == userId) );
            if (currentServer == null)
            {
                currentServer = (await _context.Server
                    .Where(x => x.ServerAssignment.AssignedUserId == null && x.Region == parameters.SelectedRegion)
                    .ToListAsync())
                    .MinBy(x => Guid.NewGuid());

                if (currentServer == null)
                {
                    return GoHome("noServersAvailable", "No servers are available in this region, please try again later.");
                }

                currentServer.ServerAssignment = new ServerAssignment
                {
                    AssignedUserId = Guid.NewGuid().ToString(),
                    GameServerPort = RandomNumberGenerator.GetInt32(currentServer.GameServerPortRangeStart, currentServer.GameServerPortRangeEnd),
                    ServerPass = Guid.NewGuid().ToString(),
                    AssignedUserIpAddress = HttpContext.Connection.RemoteIpAddress.ToString(),
                    SelectedVersion = selectedVersion
                };
                currentServer.ServerAssignmentId = currentServer.ServerAssignment.AssignedUserId;

                //NOTE: This needs to be before the trigger request and not after so the server can download it's config
                await _context.SaveChangesAsync();

                async Task AssignIpAddress()
                {
                    if (_serverConfig.ProxyV2Enabled)
                    {
                        using var _ = await _regionSemaphore.UseWaitAsync();
                        //Get latest row from database in case IP address has been assigned
                        using (await _dbContextSemaphore.UseWaitAsync())
                        {
                            regionProxy = _context.Proxy.SingleOrDefault(x => x.Region == parameters.SelectedRegion);   
                        }
                        if (regionProxy == null)
                        {
                            throw new InvalidOperationException("Region proxy was null");
                        }
                        regionProxy.IpAddress ??= await _flyService.AllocateIpAddress(regionProxy.AppName);
                        currentServer.ServerAssignment.Proxy = regionProxy;
                        using (await _dbContextSemaphore.UseWaitAsync())
                        {
                            await _context.SaveChangesAsync();
                        }
                    }   
                }
                
                var assignIpTask = AssignIpAddress();
                
                async Task StartProxy()
                {
                    if (_serverConfig.ProxyV2Enabled)
                    {
                        await _flyService.StartMachine(regionProxy.ServerId, regionProxy.AppName);
                        await _flyService.WaitMachine(regionProxy.ServerId, regionProxy.AppName);
                        _logger.LogWarning($"Starting proxy for {regionProxy.Region} {regionProxy.Id}, {regionProxy.ServerId}");
                    }
                }

                //Don't wait for proxy right now
                var startProxyTask = StartProxy();

                if (currentServer.IsTemporaryServer)
                {
                    //Do this again after ServerAssignment is saved so there's less chance of a race condition
                    Server sourceMachine;
                    using (await _dbContextSemaphore.UseWaitAsync())
                    {
                        sourceMachine = await _context.Server.FirstOrDefaultAsync(x => !x.IsTemporaryServer);
                    }

                    var newMachine = await _flyService.CopyMachine(sourceMachine.ServerId, _serverConfig.FlyBackendAppName, parameters.SelectedRegion);

                    await Task.Delay(1000);
                    
                    currentServer.PrivateIpAddress = $"[{newMachine.PrivateIp}]";
                    currentServer.ServerId = newMachine.Id;
                    using (await _dbContextSemaphore.UseWaitAsync())
                    {
                        await _context.SaveChangesAsync();
                    }

                    ((DbConfigProvider)_proxyConfigProvider).Update();
                }

                if (!currentServer.IsTemporaryServer)
                {
                    await _flyService.StartMachine(currentServer.ServerId);
                }
                
                await _flyService.WaitMachine(currentServer.ServerId);

                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        var serverUrl = $"http://{currentServer.PrivateIpAddress}:7000/";
                        _logger.LogWarning($"Sending backend request to {serverUrl}, {currentServer.ServerId}");
                        await new HttpClient { Timeout = TimeSpan.FromSeconds(15) }.GetAsync(serverUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send request to backend, try {i}", i);
                    }
                }

                await Task.WhenAll(startProxyTask, assignIpTask);
            }

            HttpContext.Session.SetKey(SessionKey.UserId, currentServer.ServerAssignment.AssignedUserId);
            return RedirectToAction("Server");
        }
        catch (Exception e)
        {
            if (currentServer != null)
            {
                try
                {
                    using (await _dbContextSemaphore.UseWaitAsync())
                    {
                        _context.DeallocateServer(currentServer);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Failed to deallocate server");
                }
            }
            _logger.LogError(e, "Failed to create server");
            return GoHome("serverCreationFailed", "Server creation failed, please try again later.");
        }
    }
    
    public async Task<ActionResult> ClearAllServers()
    {
#if !DEBUG
        return NotFound();            
#endif
        await _context.Server.ForEachAsync(_context.DeallocateServer);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    public async Task<ActionResult> GetServerInfo()
    {
        var sessionId = HttpContext.Session.GetKey(SessionKey.UserId);
        if (sessionId == null)
        {
            return BadRequest("You do not currently have a server, please create one below.");
        }

        var server = await _context.Server.SingleOrDefaultAsync(x => x.ServerAssignment.AssignedUserId == sessionId);
        
        if (_memoryCache.TryGetValue(CacheKeys.ExpiredAssignedPrefix + sessionId, out var _) && server == null)
        {
            HttpContext.Session.Remove(SessionKey.UserId.ToString());
            return StatusCode((int)HttpStatusCode.Gone, "Your server has expired.");
        }
        
        if (server == null)
        {
            return BadRequest("There was an error finding your server, please create a new one.");
        }

        return Ok(new
        {
            CurrentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            InactivityEndDate = server.ServerAssignment.LastActivity?.AddSeconds(_serverConfig.InactivityTimeout).ToUnixTimeMilliseconds(),
            EndDate = server.ServerAssignment.StartTime?.AddSeconds(_serverConfig.ServerTimeout).ToUnixTimeMilliseconds()
        });
    }

    private async Task<bool> Ping(Server server)
    {
        var client = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(1)
        };
        try
        {
            await client.GetAsync($"http://[{server.PrivateIpAddress}]:7000");
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<List<string>> GetPmVersions()
    {
        return await _memoryCache.GetOrCreateAsync(
            "PMVersions",
            async cacheEntry =>
            {
                cacheEntry.SlidingExpiration = TimeSpan.FromMinutes(5);
                var url = "https://api.github.com/repos/pmmp/update.pmmp.io/contents/channels";
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "PocketDock");
                var data = await client.GetStringAsync(url);
                var versions = JsonConvert.DeserializeObject<List<GithubFileItem>>(data);
                return versions
                    .Select(x => x.Name.Replace(".json", ""))
                    .Where(x => !x.StartsWith("3") && x != "pm3")
                    .ToList();
            });
    }

    public class GithubFileItem
    {
        public string Name { get; set; }
    }
}