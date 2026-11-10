using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Auth;
using Wms.Domain.Common;
using Wms.Domain.Entities.Identity;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Persistence.Interceptors;
using Wms.Infrastructure.Services;

namespace Wms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Interceptors
        services.AddScoped<AuditSaveChangesInterceptor>();

        // Database Context
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? "Host=localhost;Port=5432;Database=wms_db;Username=postgres;Password=postgres;";

        services.AddDbContext<WmsDbContext>((sp, options) =>
        {
            var auditInterceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(WmsDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });
            options.AddInterceptors(auditInterceptor);
        });

        services.AddScoped<IWmsDbContext>(sp => sp.GetRequiredService<WmsDbContext>());

        // ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<WmsDbContext>()
        .AddDefaultTokenProviders();

        // Infrastructure Services
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, DateTimeClock>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<Wms.Application.Features.Identity.Users.IUserService, Services.Identity.UserService>();
        services.AddScoped<Wms.Application.Features.Identity.Roles.IRoleService, Services.Identity.RoleService>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<DatabaseSeeder>();

        // Stock, Ledger, Valuation & Transaction Services (Tahap 3)
        services.AddScoped<Wms.Application.Features.Stock.DocumentNumbering.IDocumentSequenceService, Services.Stock.DocumentSequenceService>();
        services.AddScoped<Wms.Application.Features.Stock.Idempotency.IIdempotencyService, Services.Stock.IdempotencyService>();
        services.AddScoped<Wms.Application.Features.Stock.Postings.IStockPostingService, Services.Stock.StockPostingService>();
        services.AddScoped<Wms.Application.Features.Stock.GoodsReceipts.IGoodsReceiptService, Services.Stock.GoodsReceiptService>();
        services.AddScoped<Wms.Application.Features.Stock.StockOuts.IStockOutService, Services.Stock.StockOutService>();
        services.AddScoped<Wms.Application.Features.Stock.Reconciliation.IStockReconciliationService, Services.Stock.StockReconciliationService>();

        // Dashboard, Reports & Audit Log Services (Tahap 4)
        services.AddScoped<Wms.Application.Features.Dashboard.IDashboardService, Services.Dashboard.DashboardService>();
        services.AddScoped<Wms.Application.Features.Reports.IReportService, Services.Reports.ReportService>();
        services.AddScoped<Wms.Application.Features.Audit.IAuditLogService, Services.Audit.AuditLogService>();

        return services;
    }
}
