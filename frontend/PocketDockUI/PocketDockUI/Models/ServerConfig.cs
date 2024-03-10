namespace PocketDockUI.Models;

public class ServerConfig
{
    public string ApiKey { get; set; }
    public int ServerTimeout { get; set; }
    public int InactivityTimeout { get; set; }
    public bool EnableFirewallForFlyIo { get; set; }
    public bool ProxyV2Enabled { get; set; }
    public string FlyApiToken { get; set; }
    public string FlyBackendAppName { get; set; }
    public string FlyProxyAppNamePrefix { get; set; }
}