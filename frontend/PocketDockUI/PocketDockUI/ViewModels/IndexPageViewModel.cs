namespace PocketDockUI.ViewModels;

public class IndexPageViewModel
{
    public IndexPageViewModel(bool hasServer, string sessionMessage, List<string> cachedVersions)
    {
        HasServer = hasServer;
        SessionMessage = sessionMessage;
        CachedVersions = cachedVersions;
    }
    public bool HasServer { get; }
    public string SessionMessage { get; }
    public List<string> CachedVersions { get; }
    public string SelectedVersion { get; set; } = "stable";
}