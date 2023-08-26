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

namespace PocketDockUI.Controllers;

public class HomeController : BaseController
{
    private readonly PocketDockContext _context;
    private readonly RecaptchaService _recaptcha;
    private readonly IMemoryCache _memoryCache;
    private readonly ProcessManager _processManager;
    private readonly ServerConfig _serverConfig;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger,
        PocketDockContext context,
        RecaptchaService recaptcha,
        IOptions<ServerConfig> serverConfig,
        IMemoryCache memoryCache,
        ProcessManager processManager) : base(logger)
    {
        _logger = logger;
        _context = context;
        _recaptcha = recaptcha;
        _memoryCache = memoryCache;
        _serverConfig = serverConfig.Value;
        _processManager = processManager;
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
        var viewModel = new IndexPageViewModel(hasServer, sessionMessage, cachedVersions);
        return View(viewModel);
    }

    public async Task<IActionResult> Server()
    {
        var sessionId = HttpContext.Session.GetKey(SessionKey.UserId);
        if (sessionId == null)
        {
            return GoHome("noSessionFound2", "You do not currently have a server, please create one below.");

        }
        var server = await _context.Server.SingleOrDefaultAsync(x => x.ServerAssignment.AssignedUserId == sessionId);
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
                    .Where(x => x.ServerAssignment.AssignedUserId == null)
                    .ToListAsync())
                    .MinBy(x => Guid.NewGuid());

                if (currentServer == null)
                {
                    return GoHome("noServersAvailable", "No servers are available, please try again later.");
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

                if (_serverConfig.ProxyEnabled)
                {
                    await _processManager.LaunchProcess(currentServer);   
                }

                //NOTE: This needs to be before the trigger request and not after so the server can download it's config
                await _context.SaveChangesAsync();
            
                //Fly.io has servers that have Wake on Request, so this will turn on the server.
                var triggerUrl = $"http://{currentServer.TriggerIpAddress}:{currentServer.TriggerPort}/";
                _logger.LogWarning($"Sending request to {triggerUrl}, {currentServer.ServerId}");
                var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                await client.GetAsync(triggerUrl);
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
                    _context.DeallocateServer(currentServer);
                    await _context.SaveChangesAsync();
                    _processManager.StopProcess(currentServer);
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