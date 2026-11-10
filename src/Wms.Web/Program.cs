using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using MudBlazor.Services;
using Serilog;
using Wms.Application;
using Wms.Infrastructure;
using Wms.Infrastructure.Persistence;
using Wms.Web.Authorization;
using Wms.Web.Components;
using Wms.Web.Endpoints;
using Wms.Web.Middlewares;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog configuration
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

    // Add Clean Architecture Layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // UI & Blazor Services
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddMudServices();

    // Authentication & Cookie Policy
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/login?error=access_denied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

    // Policy-based Authorization with Dynamic Permission Provider
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

    // Rate Limiting (Tahap 1: Proteksi Brute Force Login)
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("login-limiter", opt =>
        {
            opt.PermitLimit = 10;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueLimit = 0;
        });
    });

    // Health Checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<WmsDbContext>("database_health");

    var app = builder.Build();

    // Auto migrate and seed on startup
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        try
        {
            var dbContext = services.GetRequiredService<WmsDbContext>();
            var seeder = services.GetRequiredService<DatabaseSeeder>();

            // If database is reachable, ensure migrated and seeded
            if (dbContext.Database.CanConnect())
            {
                logger.LogInformation("Database connected. Running migrations and seeders...");
                await seeder.SeedAsync();
                logger.LogInformation("Database migrations and seeding completed successfully.");
            }
            else
            {
                logger.LogWarning("Database connection could not be established immediately. Verify your connection string.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding or migrating the database.");
        }
    }

    // Middleware Pipeline
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAntiforgery();

    app.UseAuthentication();
    app.UseAuthorization();

    // Endpoints
    app.MapHealthChecks("/healthz");
    app.MapAccountEndpoints();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
