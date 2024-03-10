using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PocketDockUI.Data.Models;

public class Server
{
    public int Id { get; set; }
    public string PrivateIpAddress { get; set; }
    public string ServerId { get; set; }
    public int GameServerPortRangeStart { get; set; }
    public int GameServerPortRangeEnd { get; set; }
    public ServerAssignment ServerAssignment { get; set; }
    public string ServerAssignmentId { get; set; }
    [Required]
    public string Region { get; set; }
    public bool IsTemporaryServer { get; set; }
}

public class ServerAssignment
{
    [Key]
    public string AssignedUserId { get; set; }
    public int? GameServerPort { get; set; }
    public string AssignedUserIpAddress { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? LastActivity { get; set; }
    public string ServerPass { get; set; }
    public string SelectedVersion { get; set; }
    public Proxy Proxy { get; set; }
    public int? ProxyId { get; set; }
}

public class Proxy
{
    public int Id { get; set; }
    public string AppName { get; set; }
    public string Region { get; set; }
    public string IpAddress { get; set; }
    public string ServerId { get; set; }
    public string DisplayName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}