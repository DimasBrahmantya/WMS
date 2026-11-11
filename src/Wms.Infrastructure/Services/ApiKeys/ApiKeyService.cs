using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.ApiKeys;
using Wms.Domain.Common;
using Wms.Domain.Entities.System;

namespace Wms.Infrastructure.Services.ApiKeys;

public class ApiKeyService : IApiKeyService
{
    private readonly IWmsDbContext _context;
    private readonly IClock _clock;

    public ApiKeyService(IWmsDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ApiKeyDto>> GetApiKeysAsync(CancellationToken cancellationToken = default)
    {
        var keys = await _context.ApiKeys
            .OrderByDescending(k => k.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return keys.Select(MapToDto).ToList();
    }

    public async Task<Result<CreateApiKeyResultDto>> CreateApiKeyAsync(
        CreateApiKeyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<CreateApiKeyResultDto>.Failure("Nama API Key wajib diisi.");

        var rawBytes = RandomNumberGenerator.GetBytes(24);
        var secretHex = Convert.ToHexString(rawBytes).ToLowerInvariant();
        var plainKey = $"wms_live_{secretHex}";
        var prefix = plainKey.Substring(0, 16);
        var hash = ComputeHash(plainKey);

        var scopes = string.Join(",", command.Scopes.Where(s => !string.IsNullOrWhiteSpace(s)));

        var apiKey = new ApiKey
        {
            Name = command.Name.Trim(),
            KeyPrefix = prefix,
            KeyHash = hash,
            Scopes = scopes,
            ExpiresAtUtc = command.ExpiresAtUtc,
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow
        };

        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateApiKeyResultDto>.Success(new CreateApiKeyResultDto
        {
            ApiKey = MapToDto(apiKey),
            PlainSecretKey = plainKey
        });
    }

    public async Task<Result> RevokeApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = await _context.ApiKeys.FindAsync(new object[] { id }, cancellationToken);
        if (key == null)
            return Result.Failure("API Key tidak ditemukan.");

        key.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<ApiKeyDto?> ValidateKeyAsync(string plainKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainKey)) return null;

        var hash = ComputeHash(plainKey.Trim());
        var now = _clock.UtcNow;

        var key = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == hash, cancellationToken);

        if (key == null || !key.IsValid(now)) return null;

        key.LastUsedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(key);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static ApiKeyDto MapToDto(ApiKey k)
    {
        return new ApiKeyDto
        {
            Id = k.Id,
            Name = k.Name,
            KeyPrefix = k.KeyPrefix,
            Scopes = k.Scopes,
            ExpiresAtUtc = k.ExpiresAtUtc,
            IsActive = k.IsActive,
            LastUsedAtUtc = k.LastUsedAtUtc,
            CreatedAtUtc = k.CreatedAtUtc
        };
    }
}

