using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;
using OLRTLabSim.Data;
using OLRTLabSim.Engine;
using OLRTLabSim.Services;
using System.Threading.Tasks;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting web application");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    Database.InitDb();

    // Add Services
    builder.Services.AddSingleton<ModbusRuntimeManager>();
    builder.Services.AddSingleton<BacnetRuntimeManager>();
    builder.Services.AddSingleton<Dnp3RuntimeManager>();

    // Background Engine
    builder.Services.AddHostedService<SimulationEngine>();

    // Authentication and Authorization
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = 401;
                }
                else
                {
                    context.Response.Redirect("/login");
                }
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = 403;
                }
                else
                {
                    context.Response.Redirect("/");
                }
                return Task.CompletedTask;
            };
            options.Cookie.Name = "OLRTLabSim_Auth";
        });

    builder.Services.AddAuthorization();

    builder.Services.AddControllersWithViews().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = new OLRTLabSim.Helpers.SnakeCaseNamingPolicy();
    });

    var app = builder.Build();

    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Endpoints fallback
    app.MapGet("/login", async context =>
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync("Pages/login.html");
    });

    app.MapGet("/change_password", async context =>
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Response.Redirect("/login");
            return;
        }
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync("Pages/change_password.html");
    });

    app.MapGet("/admin", async context =>
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Response.Redirect("/login");
            return;
        }
        if (!context.User.IsInRole("admin"))
        {
            context.Response.Redirect("/");
            return;
        }
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync("Pages/admin.html");
    });

    // Explicitly map / to the index page in Pages
    app.MapGet("/", async context =>
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Response.Redirect("/login");
            return;
        }

        if (context.User.HasClaim("NeedsPasswordChange", "1"))
        {
            context.Response.Redirect("/change_password");
            return;
        }

        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync("Pages/index.html");
    });

    app.MapGet("/status", async context =>
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync("Pages/bacnet_status.html");
    });


    app.MapGet("/settings", async context =>
    {
        if (!context.User.IsInRole("admin"))
        {
            context.Response.Redirect("/");
            return;
        }
        await context.Response.SendFileAsync("Pages/settings.html");
    });

    app.MapGet("/logs", async context =>
    {
        if (!context.User.IsInRole("admin"))
        {
            context.Response.Redirect("/");
            return;
        }
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync("Pages/logs.html");
    });

app.Run();
}
catch (System.Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
