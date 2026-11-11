using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities.Exports;
using Wms.Domain.Entities.Notifications;

namespace Wms.Infrastructure.Persistence.Configurations;

public static class NotificationAndExportConfigurations
{
    public static void Configure(ModelBuilder builder)
    {
        builder.ApplyConfiguration(new AppNotificationConfiguration());
        builder.ApplyConfiguration(new ExportJobConfiguration());
    }
}

public class AppNotificationConfiguration : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> builder)
    {
        builder.ToTable("app_notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.LinkUrl).HasMaxLength(500);
        builder.Property(n => n.DedupKey).HasMaxLength(250).IsRequired();
        builder.Property(n => n.TargetRole).HasMaxLength(100);
        builder.Property(n => n.UserId).HasMaxLength(100);

        builder.HasIndex(n => new { n.UserId, n.Status });
        builder.HasIndex(n => new { n.DedupKey, n.Status });
    }
}

public class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.ToTable("export_jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.RequestedByUserId).HasMaxLength(100).IsRequired();
        builder.Property(j => j.RequestedByUserName).HasMaxLength(150).IsRequired();
        builder.Property(j => j.ReportType).HasMaxLength(100).IsRequired();
        builder.Property(j => j.FilterPayloadJson).HasColumnType("text");
        builder.Property(j => j.FilePath).HasMaxLength(500);
        builder.Property(j => j.FileName).HasMaxLength(250);
        builder.Property(j => j.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(j => new { j.RequestedByUserId, j.Status });
        builder.HasIndex(j => j.ExpiresAtUtc);
    }
}
