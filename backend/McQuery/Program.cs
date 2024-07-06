using System.Collections;

string ip = "::1";

var port = GetEnvIntOrThrow("PMPORT");
var serverTimeout = GetEnvIntOrThrow("SERVER_TIMEOUT");
var inactivityTimeout = GetEnvIntOrThrow("INACTIVITY_TIMEOUT");
var baseUrl = GetEnvOrThrow("API_BASE_URL").TrimEnd('/');
var hostname = GetEnvOrThrow("HOSTNAME");
var remoteServerPass = GetEnvOrThrow("REMOTE_SERVER_PASS");

var queryService = new QueryService(port, ip);
    
var serverTimeoutDate = DateTime.Now.AddSeconds(serverTimeout);
var inactivityTimeoutDate = DateTime.Now.AddSeconds(inactivityTimeout);
var hasPlayers = false;

try
{
    await UpdateStats(false);
}
catch (Exception e)
{
    Console.WriteLine(e.Message);
}

Console.WriteLine("Starting loop");

while (true)
{
    try
    {
        var numPlayers = await queryService.GetPlayers();
        if (numPlayers > 0)
        {
            inactivityTimeoutDate = DateTime.Now.AddSeconds(inactivityTimeout);
            if (!hasPlayers)
            {
                Console.WriteLine("Resetting timer");
                await UpdateStats(true);
                hasPlayers = true;
            }
        }
        else
        {
            if (hasPlayers)
            {
                Console.WriteLine("Starting inactivity timer");
                await UpdateStats(false);
                hasPlayers = false;
            }
        }
    }
    catch (Exception e)
    {
        Console.WriteLine("Query failed:" + e.Message);
    }

    if (DateTime.Now > serverTimeoutDate || DateTime.Now > inactivityTimeoutDate)
    {
        break;
    }

    if (DateTimeOffset.Now.Second % 15 == 0)
    {
        Console.WriteLine($"{serverTimeoutDate - DateTime.Now}, {inactivityTimeoutDate - DateTime.Now}");
    }
    //Ping once a minute to notify that the server is alive, and also to keep the proxy alive
    if (DateTimeOffset.Now.Second % 60 == 0)
    {
        Console.WriteLine("Updating stats");
        await UpdateStats(hasPlayers);
    }

    await Task.Delay(1000);
}

string GetEnvOrThrow(string key)
{
    var val = Environment.GetEnvironmentVariable(key);
    Console.WriteLine("  {0} = {1}", key, val);
    if (val == null)
    {
        throw new ArgumentNullException(key);
    }

    return val;
}

int GetEnvIntOrThrow(string key)
{
    var val = GetEnvOrThrow(key);
    if (!int.TryParse(val, out var intVal))
    {
        throw new ArgumentException($"Environment variable {key} must be an integer");
    }

    return intVal;
}

async Task UpdateStats(bool hasPlayers)
{
    Console.WriteLine("Sending request");
    var url = $"{baseUrl}/api/Server/UpdateStats?serverId={hostname}&remoteServerPass={remoteServerPass}&hasPlayers={hasPlayers}";
    var res = await new HttpClient().PostAsync(url, null);
    Console.WriteLine("Sent request, response code: " + res.StatusCode);
}

Console.WriteLine("Shutting down");