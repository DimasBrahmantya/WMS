using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
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
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "wms");
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
        services.AddScoped<Wms.Application.Features.Audit.IAppAuditService, Services.Audit.AppAuditService>();

        // Workflow Operasional (Tahap 5)
        services.AddScoped<Wms.Application.Features.Operations.PurchaseOrders.IPurchaseOrderService, Services.Operations.PurchaseOrderService>();
        services.AddScoped<Wms.Application.Features.Operations.Transfers.IStockTransferService, Services.Operations.StockTransferService>();
        services.AddScoped<Wms.Application.Features.Operations.Opname.IStockOpnameService, Services.Operations.StockOpnameService>();
        services.AddScoped<Wms.Application.Features.Operations.Returns.IStockReturnService, Services.Operations.StockReturnService>();
        services.AddScoped<Wms.Application.Features.Operations.Incidents.IStockIncidentService, Services.Operations.StockIncidentService>();

        // Notifikasi & Ekspor (Tahap 6)
        services.AddScoped<Wms.Application.Features.Notifications.INotificationService, Services.Notifications.NotificationService>();
        services.AddSingleton<Services.Exports.FileStorage.IExportFileStorage, Services.Exports.FileStorage.LocalExportFileStorage>();
        services.AddSingleton<Services.Exports.Formatters.IReportFormatter, Services.Exports.Formatters.CsvReportFormatter>();
        services.AddSingleton<Services.Exports.Formatters.IReportFormatter, Services.Exports.Formatters.ExcelReportFormatter>();
        services.AddSingleton<Services.Exports.Formatters.IReportFormatter, Services.Exports.Formatters.PdfReportFormatter>();
        services.AddScoped<Wms.Application.Features.Exports.IExportService, Services.Exports.ExportService>();

        // Fitur Lanjutan (Tahap 7)
        services.AddScoped<Wms.Application.Features.Batches.IBatchService, Services.Batches.BatchService>();
        services.AddScoped<Wms.Application.Features.Batches.IFefoService, Services.Batches.FefoService>();
        services.AddSingleton<Wms.Application.Features.Barcode.IBarcodeService, Services.Barcode.SvgBarcodeService>();
        services.AddScoped<Wms.Application.Features.WarehouseOperations.IPutawayService, Services.WarehouseOperations.PutawayService>();
        services.AddScoped<Wms.Application.Features.WarehouseOperations.IPickingService, Services.WarehouseOperations.PickingService>();
        services.AddScoped<Wms.Application.Features.SalesOrders.ISalesOrderService, Services.SalesOrders.SalesOrderService>();
        services.AddScoped<Wms.Application.Features.Imports.IDataImportService, Services.Imports.DataImportService>();
        services.AddScoped<Wms.Application.Features.ApiKeys.IApiKeyService, Services.ApiKeys.ApiKeyService>();
        services.AddScoped<Wms.Application.Features.Backup.ISystemBackupService, Services.Backup.SystemBackupService>();

        // Background Job Scheduler (Quartz.NET)
        services.AddQuartz(q =>
        {
            var stockAlertKey = new JobKey("StockAlertJob");
            q.AddJob<BackgroundJobs.StockAlertJob>(opts => opts.WithIdentity(stockAlertKey));
            q.AddTrigger(opts => opts
                .ForJob(stockAlertKey)
                .WithIdentity("StockAlertJob-Trigger")
                .WithSimpleSchedule(x => x.WithIntervalInMinutes(15).RepeatForever()));

            var overduePoKey = new JobKey("OverduePurchaseOrderJob");
            q.AddJob<BackgroundJobs.OverduePurchaseOrderJob>(opts => opts.WithIdentity(overduePoKey));
            q.AddTrigger(opts => opts
                .ForJob(overduePoKey)
                .WithIdentity("OverduePurchaseOrderJob-Trigger")
                .WithSimpleSchedule(x => x.WithIntervalInHours(1).RepeatForever()));

            var reconKey = new JobKey("ReconciliationJob");
            q.AddJob<BackgroundJobs.ReconciliationJob>(opts => opts.WithIdentity(reconKey));
            q.AddTrigger(opts => opts
                .ForJob(reconKey)
                .WithIdentity("ReconciliationJob-Trigger")
                .WithSimpleSchedule(x => x.WithIntervalInHours(12).RepeatForever()));

            var exportWorkerKey = new JobKey("BackgroundExportWorkerJob");
            q.AddJob<BackgroundJobs.BackgroundExportWorkerJob>(opts => opts.WithIdentity(exportWorkerKey));
            q.AddTrigger(opts => opts
                .ForJob(exportWorkerKey)
                .WithIdentity("BackgroundExportWorkerJob-Trigger")
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(30).RepeatForever()));

            var cleanupKey = new JobKey("CleanupExportFilesJob");
            q.AddJob<BackgroundJobs.CleanupExportFilesJob>(opts => opts.WithIdentity(cleanupKey));
            q.AddTrigger(opts => opts
                .ForJob(cleanupKey)
                .WithIdentity("CleanupExportFilesJob-Trigger")
                .WithSimpleSchedule(x => x.WithIntervalInHours(24).RepeatForever()));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }
}
