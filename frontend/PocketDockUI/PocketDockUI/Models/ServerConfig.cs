namespace PocketDockUI.Models;

public class ServerConfig
{
    public string ApiKey { get; set; }
    public int ServerTimeout { get; set; }
    public int InactivityTimeout { get; set; }
    public bool ProxyEnabled { get; set; }
    public bool EnableFirewallForFlyIo { get; set; }
}