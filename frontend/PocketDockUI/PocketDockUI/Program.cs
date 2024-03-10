using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PocketDockUI.Data;
using PocketDockUI.Models;
using PocketDockUI.Services;
using Yarp.ReverseProxy.Configuration;

using System.Net;
using Firewall;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using PocketDockUI.Controllers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();

builder.Services.Configure<ServerConfig>(builder.Configuration.GetSection(nameof(ServerConfig)));
builder.Services.Configure<RecaptchaConfig>(builder.Configuration.GetSection(nameof(RecaptchaConfig)));
builder.Services.AddScoped<RecaptchaService>();
builder.Services.AddTransient<FlyService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Add the reverse proxy to capability to the server
builder.Services.AddReverseProxy().LoadFromDb().AddTransforms<ServerPassHeaderTransform>();

builder.Services.AddDbContext<PocketDockContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresDatabase"), x => x.MigrationsAssembly(typeof(PocketDockContext).Assembly.GetName().Name)));


builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // To preserve the default behavior, capture the original delegate to call later.
        var builtInFactory = options.InvalidModelStateResponseFactory;

        options.InvalidModelStateResponseFactory = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            var data = context.ModelState.ToDictionary(kvp => 
                    kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray())
                .Where(m => m.Value.Any());
            logger.LogError(JsonConvert.SerializeObject(data));
            
            return builtInFactory(context);
        };
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PocketDockContext>();
    db.Database.Migrate();
}

((DbConfigProvider)app.Services.GetRequiredService<IProxyConfigProvider>()).Update();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();

if (app.Services.GetRequiredService<IOptions<ServerConfig>>().Value.EnableFirewallForFlyIo)
{
    app.UseWhen(context => !IsController<ServerController>(context), AllowOnlyCloudflare);

    app.UseWhen(IsController<ServerController>, app =>
    {
        app.UseFirewall(FirewallRulesEngine
            .DenyAllAccess()
            .ExceptFromIPAddressRanges(new List<CIDRNotation> { CIDRNotation.Parse("fc00::/7") }));
    });
}

app.UseStaticFiles();

app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseSessionClusterSelectionMiddleware();
});

app.Run();

bool IsController<T>(HttpContext context)
{
    return context
        .GetEndpoint()?
        .Metadata
        .GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo == typeof(T);
}

void AllowOnlyCloudflare(IApplicationBuilder app)
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor,
        ForwardedForHeaderName = "fly-client-ip",
        KnownNetworks = { new IPNetwork(IPAddress.Parse("172.16.0.0"), 16) }
    });

    var rules = FirewallRulesEngine
        .DenyAllAccess()
        .ExceptFromCloudflare();
    
    app.UseFirewall(rules, ctx =>
    {
        ctx.Response.Redirect("https://instant.pocketdock.io");
        return Task.CompletedTask;
    });

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor,
        ForwardedForHeaderName = "cf-connecting-ip",
        //By time we get here, we know that the IP is trusted
        KnownNetworks = {new IPNetwork(IPAddress.Parse("0.0.0.0"), 0)},
    });
}