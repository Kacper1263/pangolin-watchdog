using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using MudBlazor.Services;
using PangolinWatchdog.Components;
using PangolinWatchdog.Data;
using PangolinWatchdog.Helpers;
using PangolinWatchdog.Services;
using PangolinWatchdog.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<PangolinConnector>();
builder.Services.AddHostedService<LogWatcherWorker>();

// SQLite
var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "watchdog.db");
var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}
builder.Services.AddDbContextFactory<AppDbContext>(options => 
    options.UseSqlite($"Data Source={dbPath}"));
Console.WriteLine($"[INIT] Database path set to: {dbPath}");

// MudBlazor (GUI)
builder.Services.AddMudServices();

// Authentication & Authorization
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "PangolinWatchdogAuth";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState(); 

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.FormatterName = "minimal")
    .AddConsoleFormatter<MinimalConsoleFormatter, ConsoleFormatterOptions>();
builder.Logging.AddDebug();

var app = builder.Build();

// Ensure database is created and up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

AddRoutes();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var server = app.Services.GetRequiredService<IServer>();
    var addressFeature = server.Features.Get<IServerAddressesFeature>();
    
    foreach (var address in addressFeature?.Addresses ?? Enumerable.Empty<string>())
    {
        var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{time}] [INFO] GUI Listening on: {address}");
        Console.ResetColor();
    }
});

app.Run();

return;


void AddRoutes()
{
    app.MapPost("/api/login", async (HttpContext http) =>
    {
        var form = await http.Request.ReadFormAsync();
        var password = form["password"].ToString();
    
        // Get password from environment variable or use default
        var correctPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "watchdogadmin";

        if (password != correctPassword)
        {
            return Results.Redirect("/login?error=true");
        }

        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "Admin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Results.Redirect("/");
    });
    
    app.MapGet("/api/logout", async (HttpContext http) =>
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    });
}
