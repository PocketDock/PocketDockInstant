using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PocketDockUI.Data;
using PocketDockUI.Models;

namespace PocketDockUI.Services;

public class ProxyStartup : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ProcessManager _processManager;
    private readonly ServerConfig _serverConfig;

    public ProxyStartup(IServiceProvider serviceProvider, ProcessManager processManager, IOptions<ServerConfig> serverConfig)
    {
        _serviceProvider = serviceProvider;
        _processManager = processManager;
        _serverConfig = serverConfig.Value;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_serverConfig.ProxyEnabled)
        {
            return;
        }
        
        await _processManager.StopAllProcesses();
        var context = _serviceProvider.GetRequiredService<PocketDockContext>();
        var servers = await context.Server.Where(x => x.ServerAssignmentId != null).ToListAsync();
        foreach (var server in servers)
        {
            await _processManager.LaunchProcess(server);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_serverConfig.ProxyEnabled)
        {
            return;
        }
        
        await _processManager.StopAllProcesses();
    }
}