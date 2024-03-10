using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketDockUI.Data;
using PocketDockUI.Models;
using HttpMethod = Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpMethod;

namespace PocketDockUI.Services;

public class FlyService
{
    private readonly ServerConfig _serverConfig;

    public FlyService(IOptions<ServerConfig> serverConfig)
    {
        _serverConfig = serverConfig.Value;
    }
    
    public async Task<string> AllocateIpAddress(string appName)
    {
        var query = @"mutation($input: AllocateIPAddressInput!) {
                      allocateIpAddress(input: $input) {
                        ipAddress {
                          id
                          address
                          type
                          region
                          createdAt
                        }
                      }
                    }";
        var parameters = new Dictionary<string, string>
        {
            ["appId"] = appName,
            ["type"] = "v4",
            ["region"] = ""
        };

        var response = await SendGraphQlRequest(query, parameters);

        var ipAddress = response?["data"]?["allocateIpAddress"]?["ipAddress"]?["address"]?.ToString();
        
        return ipAddress ?? throw new InvalidOperationException("IP address was null");
    }

    public async Task ReleaseIpAddress(string appName, string ipAddress)
    {
        var query = @"mutation($input: ReleaseIPAddressInput!) {
                      releaseIpAddress(input: $input) {
                        clientMutationId
                      }
                    }";
        var parameters = new Dictionary<string, string>
        {
            ["appId"] = appName,
            ["ipAddressId"] = null,
            ["ip"] = ipAddress
        };

        await SendGraphQlRequest(query, parameters);
    }

    public async Task<List<ProxyApp>> GetMachineInfoForProxies()
    {
        var query = @"
                    {  
                      apps {
                        nodes {
                          name
                          ipAddresses {
                            nodes {
                              address
                            }
                          }
                          machines(active: true) {
                            nodes {
                              id
                              region
                            }
                          }
                        }
                      }
                    }";
        var machineInfo = await SendGraphQlRequest(query, null);

        return machineInfo["data"]["apps"]["nodes"]
            .Select(app => new ProxyApp
            {
                AppName = (string)app["name"],
                Region = app["machines"]["nodes"].Select(m => (string)m["region"]).FirstOrDefault(),
                IpAddress = app["ipAddresses"]["nodes"].Count() > 0
                    ? (string)app["ipAddresses"]["nodes"][0]["address"]
                    : null,
                MachineId = app["machines"]["nodes"].Select(m => (string)m["id"]).FirstOrDefault()
            })
            .ToList();
    }

    public async Task<FlyMachine> CopyMachine(string sourceMachineId, string destinationApp, string destinationRegion)
    {
        var sourceMachine = await SendMachineRequest($"https://api.machines.dev/v1/apps/{_serverConfig.FlyBackendAppName}/machines/{sourceMachineId}", HttpMethod.Get);
        var destinationMachineRequest = new JObject
        {
            //Don't use ToObject<FlyMachine>() here because we want to copy even properties that aren't in the model
            ["config"] = sourceMachine["config"],
            ["region"] = destinationRegion,
            //["skip_launch"] = true,
        };
        destinationMachineRequest["config"]["auto_destroy"] = true;
        var response = await SendMachineRequest($"https://api.machines.dev/v1/apps/{destinationApp}/machines", HttpMethod.Post, destinationMachineRequest);
        return response.ToObject<FlyMachine>();
    }
    
    public async Task StartMachine(string machineId, string appName = null)
    {
        await SendMachineRequest($"https://api.machines.dev/v1/apps/{appName ?? _serverConfig.FlyBackendAppName}/machines/{machineId}/start", HttpMethod.Post);
    }
    
    public async Task WaitMachine(string machineId, string appName = null)
    {
        await SendMachineRequest($"https://api.machines.dev/v1/apps/{appName ?? _serverConfig.FlyBackendAppName}/machines/{machineId}/wait", HttpMethod.Get);
    }
    
    private async Task<JObject> SendGraphQlRequest(string query, Dictionary<string, string> parameters)
    {
        var request = new {
            query = query,
            variables = new {
                input = parameters
            }
        };

        var jsonString = JsonConvert.SerializeObject(request);

        var client = new HttpClient();
        if (!client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + _serverConfig.FlyApiToken))
        {
            throw new InvalidOperationException("Could not add authorization header");
        }
        var requestBody = new StringContent(jsonString, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.fly.io/graphql", requestBody);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        var jObject = JObject.Parse(responseString);
        if (jObject["errors"] != null)
        {
            throw new InvalidOperationException("Request returned an error: " + responseString);
        }

        return jObject;
    }

    private async Task<JObject> SendMachineRequest(string url, HttpMethod method, JObject body = null)
    {
        if (method is HttpMethod.Get or HttpMethod.Delete && body != null)
        {
            throw new InvalidOperationException($"Cannot send body with {method.ToString().ToUpper()} request");
        }

        var client = new HttpClient();
        if (!client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + _serverConfig.FlyApiToken))
        {
            throw new InvalidOperationException("Could not add authorization header");
        }

        StringContent requestBody = null;
        if (body != null)
        {
            requestBody = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
        }

        var response = method switch
        {
            HttpMethod.Get => await client.GetAsync(url),
            HttpMethod.Post => await client.PostAsync(url, requestBody),
            HttpMethod.Put => await client.PutAsync(url, requestBody),
            HttpMethod.Delete => await client.DeleteAsync(url),
            _ => throw new InvalidOperationException("Invalid http method")
        };
        response.EnsureSuccessStatusCode();
        return JObject.Parse(await response.Content.ReadAsStringAsync());
    }
}

public class ProxyApp
{
    public string AppName { get; set; }
    public string Region { get; set; }
    public string IpAddress { get; set; }
    public string MachineId { get; set; }
}