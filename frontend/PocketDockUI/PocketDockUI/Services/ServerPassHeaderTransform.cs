using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PocketDockUI.Data;
using PocketDockUI.Extensions;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace PocketDockUI.Services;

public class ServerPassHeaderTransform : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) {}

    public void ValidateCluster(TransformClusterValidationContext context) {}

    public void Apply(TransformBuilderContext builderContext)
    {
        builderContext.AddRequestHeaderRemove("Authorization");
        builderContext.AddRequestTransform(async transformContext =>
        {
            var context = transformContext.HttpContext.RequestServices.GetRequiredService<PocketDockContext>();
            string userId = transformContext.HttpContext.Session.GetKey(SessionKey.UserId);
            var server = await context.Server.SingleOrDefaultAsync(x => x.ServerAssignment.AssignedUserId == userId);
            var computedPass = Convert.ToBase64String(Encoding.ASCII.GetBytes($"pocketdock:{server!.ServerAssignment.ServerPass}"));
            transformContext.ProxyRequest.Headers.Add("Authorization", "Basic " + computedPass);
        });

        builderContext.AddResponseHeaderRemove("WWW-Authenticate", ResponseCondition.Always);
    }
}