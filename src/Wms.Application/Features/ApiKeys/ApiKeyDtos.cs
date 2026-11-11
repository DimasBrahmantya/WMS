namespace Wms.Application.Features.ApiKeys;

public class ApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public bool HasScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(Scopes)) return false;
        var parts = Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }
}

public class CreateApiKeyCommand
{
    public string Name { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public DateTime? ExpiresAtUtc { get; set; }
}

public class CreateApiKeyResultDto
{
    public ApiKeyDto ApiKey { get; set; } = null!;
    public string PlainSecretKey { get; set; } = string.Empty;
}

