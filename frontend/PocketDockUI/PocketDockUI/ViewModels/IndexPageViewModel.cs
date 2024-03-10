using PocketDockUI.Data.Models;
using PocketDockUI.Models;

namespace PocketDockUI.ViewModels;

public class IndexPageViewModel
{
    public IndexPageViewModel(bool hasServer, string sessionMessage, List<string> cachedVersions, string selectedRegion, List<ProxyDto> availableRegions)
    {
        HasServer = hasServer;
        SessionMessage = sessionMessage;
        CachedVersions = cachedVersions;
        SelectedRegion = selectedRegion;
        AvailableRegions = availableRegions;
    }
    public bool HasServer { get; }
    public string SessionMessage { get; }
    public List<string> CachedVersions { get; }
    public List<ProxyDto> AvailableRegions { get; set; }
    public string SelectedVersion { get; set; } = "stable";
    public string SelectedRegion { get; set; }
}