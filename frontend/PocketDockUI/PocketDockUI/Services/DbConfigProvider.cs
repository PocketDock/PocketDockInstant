using System.Text;
using Microsoft.Extensions.Primitives;
using PocketDockUI.Data;
using Yarp.ReverseProxy.Configuration;

namespace PocketDockUI.Services;

public static class DbConfigProviderExtensions
{
    public static IReverseProxyBuilder LoadFromDb(this IReverseProxyBuilder builder)
    {
        builder.Services.AddSingleton<IProxyConfigProvider, DbConfigProvider>();
        return builder;
    }
}

public static class RouteConfigExtension
{
    public static bool IsApiRoute(this RouteConfig routeConfig)
    {
        return routeConfig.Metadata?["Type"] == DbConfigProvider.ApiTypeString;
    }
}

public class DbConfigProvider : IProxyConfigProvider
{
    private readonly IServiceProvider _serviceProvider;
    private volatile DbConfig _config;
    public const string ApiTypeString = "Api";
    public const string PageTypeString = "Page";

    public DbConfigProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _config = new DbConfig(new List<RouteConfig>(), new List<ClusterConfig>());
    }

    public Dictionary<string, string> ApiType => new() { ["Type"] = ApiTypeString };
    public Dictionary<string, string> PageType => new() { ["Type"] = PageTypeString };

    public IProxyConfig GetConfig() => _config;

    public void Update()
    {
        using var serviceScope = _serviceProvider.CreateScope();
        var services = serviceScope.ServiceProvider;

        var context = services.GetRequiredService<PocketDockContext>();
        var servers = context.Server.Where(x => (!x.IsTemporaryServer || x.ServerAssignmentId != null) && x.ServerId != null).ToList();
        var clusters = new[]
        {
            new ClusterConfig()
            {
                ClusterId = "Console",
                Destinations = servers.ToDictionary(x => x.ServerId, x => new DestinationConfig()
                {
                    Address = $"http://{x.PrivateIpAddress}:7000"
                })
            },
            new ClusterConfig()
            {
                ClusterId = "FileBrowser",
                Destinations = servers.ToDictionary(x => x.ServerId, x => new DestinationConfig()
                {
                    Address = $"http://{x.PrivateIpAddress}:8000"
                })
            }
        };
        
        var oldConfig = _config;
        _config = new DbConfig(GetRoutes(), clusters);
        oldConfig.SignalChange();
    }
    
    private RouteConfig[] GetRoutes()
    {
        return new[]
        {
            new RouteConfig()
            {
                RouteId = "FileBrowserRoute",
                ClusterId = "FileBrowser",
                Match = new RouteMatch
                {
                    Path = "/files/{*any}"
                },
                Metadata = PageType
            },
            new RouteConfig()
            {
                RouteId = "WebsocketRoute",
                ClusterId = "Console",
                Match = new RouteMatch
                {
                    Path = "/console/{*any}"
                },
                Transforms = new []
                {
                    new Dictionary<string, string> { ["PathRemovePrefix"] = "/console" },
                    new Dictionary<string, string> { ["RequestHeaderRemove"] = "Origin" }
                },
                Metadata = PageType
            },
        };
    }

    private class DbConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public DbConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken { get; }

        internal void SignalChange()
        {
            _cts.Cancel();
        }
    }
}