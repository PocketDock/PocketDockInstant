using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PocketDockUI.Data.Models;

public class Server
{
    public int Id { get; set; }
    public string PrivateIpAddress { get; set; }
    public string TriggerIpAddress { get; set; }
    public string Domain { get; set; }
    public string ServerId { get; set; }
    public int TriggerPort { get; set; }
    public int GameServerPortRangeStart { get; set; }
    public int GameServerPortRangeEnd { get; set; }
    public ServerAssignment ServerAssignment { get; set; }
    public string ServerAssignmentId { get; set; }
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
}