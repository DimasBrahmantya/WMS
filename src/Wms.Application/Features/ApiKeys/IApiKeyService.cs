using Wms.Application.Common.Models;

namespace Wms.Application.Features.ApiKeys;

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeyDto>> GetApiKeysAsync(CancellationToken cancellationToken = default);
    Task<Result<CreateApiKeyResultDto>> CreateApiKeyAsync(CreateApiKeyCommand command, CancellationToken cancellationToken = default);
    Task<Result> RevokeApiKeyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiKeyDto?> ValidateKeyAsync(string plainKey, CancellationToken cancellationToken = default);
}

