using System.Net;
using Microsoft.EntityFrameworkCore;
using PocketDockUI.Data;
using PocketDockUI.Extensions;
using Yarp.ReverseProxy.Model;

namespace PocketDockUI.Services;

public class SessionClusterSelectionMiddleware
{
    private readonly RequestDelegate _next;

    public SessionClusterSelectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext httpContext, PocketDockContext dbContext, ILogger<SessionClusterSelectionMiddleware> logger)
    {
        var proxyFeature = httpContext.GetReverseProxyFeature();
        string userId = httpContext.Session.GetKey(SessionKey.UserId);

        var isApi = proxyFeature.Route.Config.IsApiRoute();
        
        async Task GoHome(string key, string message)
        {
            if (isApi)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await httpContext.Response.WriteAsync(message);
            }
            else
            {
                httpContext.Response.Redirect("/?" + key);
                httpContext.Session.AddBanner(message);
                await httpContext.Response.WriteAsync("");   
            }
            logger.LogWarning($"Error while proxying: {key}: {message}");
        }
        
        if (string.IsNullOrWhiteSpace(userId))
        {
            await GoHome("invalidSession", "You do not currently have a server.");
            return;
        }
        var server = await dbContext.Server.SingleOrDefaultAsync(x => x.ServerAssignment.AssignedUserId == userId);
        if (server == null)
        {
            await GoHome("noServerAssigned", "You do not currently have a server.");
            return;
        }
        var destination = proxyFeature.AvailableDestinations.SingleOrDefault(x => x.DestinationId == server!.ServerId);
        if (destination == null)
        {
            await GoHome("clusterNotFound", "There was an error finding your server, please create a new one.");
            return;
        }
        proxyFeature.AvailableDestinations = new List<DestinationState> { destination }.AsReadOnly();
        await _next(httpContext);
    }
}

public static class SessionClusterSelectionMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionClusterSelectionMiddleware(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionClusterSelectionMiddleware>();
    }
}