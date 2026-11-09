using Microsoft.Extensions.Logging;
using Wms.Application.Common.Interfaces;

namespace Wms.Infrastructure.Services;

public class LocalFileStorage : IFileStorage
{
    private readonly string _baseStoragePath;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(ILogger<LocalFileStorage> logger)
    {
        _logger = logger;
        _baseStoragePath = Path.Combine(AppContext.BaseDirectory, "storage");
        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string bucketName, CancellationToken cancellationToken = default)
    {
        var bucketDir = Path.Combine(_baseStoragePath, bucketName);
        if (!Directory.Exists(bucketDir))
        {
            Directory.CreateDirectory(bucketDir);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(bucketDir, uniqueFileName);

        using var destinationStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(destinationStream, cancellationToken);

        _logger.LogInformation("File uploaded to {Path}", fullPath);
        return Path.Combine(bucketName, uniqueFileName).Replace('\\', '/');
    }

    public Task<Stream?> DownloadFileAsync(string filePath, string bucketName, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_baseStoragePath, filePath);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteFileAsync(string filePath, string bucketName, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_baseStoragePath, filePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted from {Path}", fullPath);
        }
        return Task.CompletedTask;
    }
}
