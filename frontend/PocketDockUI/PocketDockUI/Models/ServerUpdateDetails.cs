namespace PocketDockUI.Models;

public class ServerUpdateDetails
{
    public string TriggerIp { get; set; }
    public string Domain { get; set; }
    public IFormFile Machines { get; set; }
}