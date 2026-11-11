using Microsoft.AspNetCore.Hosting;

namespace Wms.Infrastructure.Services.Exports.FileStorage;

public interface IExportFileStorage
{
    Task<string> SaveFileAsync(string fileName, byte[] content, CancellationToken cancellationToken = default);
    Task<byte[]?> ReadFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);
    string GetStorageDirectory();
}

public class LocalExportFileStorage : IExportFileStorage
{
    private readonly string _storageDir;

    public LocalExportFileStorage(IWebHostEnvironment? env = null)
    {
        var basePath = env?.ContentRootPath ?? AppContext.BaseDirectory;
        _storageDir = Path.Combine(basePath, "App_Data", "Exports");
        if (!Directory.Exists(_storageDir))
        {
            Directory.CreateDirectory(_storageDir);
        }
    }

    public string GetStorageDirectory() => _storageDir;

    public async Task<string> SaveFileAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_storageDir, fileName);
        await File.WriteAllBytesAsync(filePath, content, cancellationToken);
        return filePath;
    }

    public async Task<byte[]?> ReadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return null;
        return await File.ReadAllBytesAsync(filePath, cancellationToken);
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Ignore transient file lock during delete
            }
        }
        return Task.CompletedTask;
    }
}

