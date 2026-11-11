namespace Wms.Domain.Entities.System;

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty; // e.g. "wms_live_ab12cd34"
    public string KeyHash { get; set; } = string.Empty;   // SHA-256 hash of secret key
    public string Scopes { get; set; } = string.Empty;    // Comma-separated scopes
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsExpired(DateTime nowUtc) => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= nowUtc;

    public bool IsValid(DateTime nowUtc) => IsActive && !IsExpired(nowUtc);

    public bool HasScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(Scopes)) return false;
        var parts = Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }
}

