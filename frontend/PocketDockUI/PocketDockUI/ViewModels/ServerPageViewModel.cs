using PocketDockUI.Data.Models;

namespace PocketDockUI.ViewModels;

public class ServerPageViewModel
{
    public ServerPageViewModel(Server server)
    {
        Server = server;
    }
    public Server Server { get; set; }
}