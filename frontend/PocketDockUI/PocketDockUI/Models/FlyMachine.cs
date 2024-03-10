using Newtonsoft.Json;
using PocketDockUI.Controllers;

namespace PocketDockUI.Models;

public class FlyMachine
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("private_ip")]
    public string PrivateIp { get; set; }

    [JsonProperty("config")]
    public FlyMachineConfig Config { get; set; }
    
    [JsonProperty("region")]
    public string Region { get; set; }
    
    [JsonProperty("skip_launch")]
    public bool SkipLaunch { get; set; }
}

public class FlyMachineConfig
{
    [JsonProperty("services")]
    public List<FlyMachineService> Services { get; set; }
}

public class FlyMachineService
{
    [JsonProperty("protocol")]
    public string Protocol { get; set; }

    [JsonProperty("internal_port")]
    public int InternalPort { get; set; }

    [JsonProperty("ports")]
    public List<FlyMachinePortInfo> Ports { get; set; }
}

public class FlyMachinePortInfo
{
    [JsonProperty("port")]
    public int Port { get; set; }
}

public class FlyRegionInfo
{ 
    [JsonProperty("code")]
    public string Code { get; set; }
    
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("latitude")]
    public double Latitude { get; set; }
    
    [JsonProperty("longitude")]
    public double Longitude { get; set; }
    
    [JsonProperty("requiresPaidPlan")]
    public bool RequiresPaidPlan { get; set; }
}