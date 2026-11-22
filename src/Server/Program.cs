using System.Security.Claims;
using System.Text.Json.Serialization;
using GoGoaRentals.Entities.Data;
using GoGoaRentals.Server.HealthChecks;
using GoGoaRentals.Server.Infrastructure;
using GoGoaRentals.Services;
using GoGoaRentals.Services.Auth;
using GoGoaRentals.Services.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Set it in appsettings.Development.json locally " +
        "or via the ConnectionStrings__DefaultConnection environment variable in deployed environments.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.CommandTimeout(30)));

// Uploaded documents live outside the web root and are only served through authorized endpoints.
var fileStorageRoot = builder.Configuration["Storage:LocalRoot"] is { Length: > 0 } configuredRoot
    ? Path.GetFullPath(configuredRoot, builder.Environment.ContentRootPath)
    : Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads");
builder.Services.AddApplicationServices(fileStorageRoot);
builder.Services.Configure<BookingPolicyOptions>(builder.Configuration.GetSection(BookingPolicyOptions.Section));
builder.Services.Configure<AllocationOptions>(builder.Configuration.GetSection(AllocationOptions.Section));

var authSection = builder.Configuration.GetSection("Authentication");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = authSection["CookieName"] ?? "ggr.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Dev runs over plain HTTP behind the Vite proxy; everywhere else the cookie is HTTPS-only.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(authSection.GetValue("SessionLifetimeMinutes", 720));
        options.SlidingExpiration = true;
        // This is an API: answer with status codes instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        // Re-check the account on every request so a suspended or deleted user loses access immediately,
        // and a role change takes effect without waiting for the cookie to expire.
        options.Events.OnValidatePrincipal = async context =>
        {
            var principal = context.Principal!;
            var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
            var user = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? await authService.GetActiveUserAsync(userId, context.HttpContext.RequestAborted)
                : null;
            if (user is null
                || !principal.IsInRole(user.Role.ToString())
                || principal.FindFirstValue(GoGoaRentals.Server.Controllers.AuthController.RentalPartnerIdClaim) != user.RentalPartnerId?.ToString())
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ServiceExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Local convenience only: a known admin so the admin screens can be tried out.
    var seed = app.Configuration.GetSection("DevSeed");
    if (!string.IsNullOrWhiteSpace(seed["AdminEmail"]) && !string.IsNullOrWhiteSpace(seed["AdminPassword"]))
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .EnsureAdminAsync("Admin", seed["AdminEmail"]!, seed["AdminPassword"]!);
    }
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // In development the Vite dev server proxies to plain HTTP; redirecting would break the proxy.
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Hosted environments: the built React app is published into wwwroot and served by this same site,
// so /api calls stay same-origin. Locally, wwwroot is empty and the Vite dev server serves the client.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health");
// Client-side routes (e.g. /bookings/12) fall back to the React app.
app.MapFallbackToFile("index.html");

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program;
