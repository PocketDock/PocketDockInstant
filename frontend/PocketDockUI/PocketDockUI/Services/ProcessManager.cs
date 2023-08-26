using System.Diagnostics;
using System.Net;
using PocketDockUI.Data.Models;

namespace PocketDockUI.Services;

public class ProcessManager
{
    private readonly ILogger<ProcessManager> _logger;
    private readonly Dictionary<string, Process> _runningProcesses = new();

    public ProcessManager(ILogger<ProcessManager> logger)
    {
        _logger = logger;
    }

    public async Task LaunchProcess(Server server)
    {
        if (_runningProcesses.ContainsKey(server.ServerId))
        {
            _logger.LogWarning($"Tried starting proxy for {server.ServerId}, but a proxy has already been started.");
            return;
        }
        var process = new Process();
        process.StartInfo.FileName = "/docker-proxy-wrapper"; // Replace with the actual executable name
        process.StartInfo.Arguments = await GetCommand(server);
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.ErrorDataReceived += (sender, e) => _logger.LogError($"Docker-proxy error from server {server.ServerId}/{server.ServerAssignmentId}: {e.Data}");

        process.Start();
        process.BeginErrorReadLine();

        var successfullyLaunched = false;
        
        for (var i = 0; i < 5; i++)
        {
            if (!process.StandardOutput.EndOfStream)
            {
                if (await process.StandardOutput.ReadLineAsync() == "0")
                {
                    successfullyLaunched = true;
                    break;
                }    
            }
        }
        
        if (!successfullyLaunched)
        {
            StopProcess(server);
            throw new InvalidOperationException($"Failed to start proxy for {server.ServerId}.");
        }

        _runningProcesses.Add(server.ServerId, process);
    }

    public void StopProcess(Server server)
    {
        _logger.LogInformation("Stopping proxy");
        if (!_runningProcesses.TryGetValue(server.ServerId, out var process))
        {
            _logger.LogWarning($"Tried stopping proxy for {server.ServerId}, but no proxy was found.");
            return;
        }
        if (!process.HasExited)
        {
            process.Kill();
        }
        process.Dispose();
        _runningProcesses.Remove(server.ServerId);
    }

    public async Task StopAllProcesses()
    {
        //https://fly.io/docs/app-guides/udp-and-tcp/
        var bindIp = (await Dns.GetHostEntryAsync("fly-global-services")).AddressList[0];
        Process.GetProcessesByName("docker-proxy").Where(x => x.StartInfo.Arguments.Contains(bindIp.ToString())).ToList().ForEach(x => x.Kill());
        _runningProcesses.Clear();
    }

    private async Task<string> GetCommand(Server server)
    {
        //https://fly.io/docs/app-guides/udp-and-tcp/
        var bindIp = (await Dns.GetHostEntryAsync("fly-global-services")).AddressList[0];
        return $"-proto udp -host-ip {bindIp} -host-port {server.ServerAssignment.GameServerPort} -container-ip {server.PrivateIpAddress.Trim('[', ']')} -container-port {server.ServerAssignment.GameServerPort}";
    }
}