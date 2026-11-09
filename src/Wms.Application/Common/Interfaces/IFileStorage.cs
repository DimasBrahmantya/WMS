namespace Wms.Application.Common.Interfaces;

public interface IFileStorage
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string bucketName, CancellationToken cancellationToken = default);
    Task<Stream?> DownloadFileAsync(string filePath, string bucketName, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string filePath, string bucketName, CancellationToken cancellationToken = default);
}
