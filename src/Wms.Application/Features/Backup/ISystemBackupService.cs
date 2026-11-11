namespace Wms.Application.Features.Backup;

public interface ISystemBackupService
{
    Task<BackupResultDto> CreateSnapshotBackupAsync(string exportedBy, CancellationToken cancellationToken = default);
}

