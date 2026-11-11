using Wms.Application.Common.Models;

namespace Wms.Application.Features.Imports;

public interface IDataImportService
{
    Task<ImportValidationResultDto> ValidateProductImportAsync(
        Stream fileStream,
        string fileExtension,
        CancellationToken cancellationToken = default);

    Task<ImportValidationResultDto> ValidateInitialStockImportAsync(
        Stream fileStream,
        string fileExtension,
        CancellationToken cancellationToken = default);

    Task<Result<ImportExecutionResultDto>> CommitImportAsync(
        CommitImportCommand command,
        Guid userId,
        CancellationToken cancellationToken = default);
}

