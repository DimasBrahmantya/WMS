using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Wms.Application.Features.ApiKeys;

namespace Wms.Web.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string HeaderName = "X-API-Key";
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var extractedApiKey) || string.IsNullOrWhiteSpace(extractedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var keyDto = await _apiKeyService.ValidateKeyAsync(extractedApiKey!);
        if (keyDto == null)
        {
            return AuthenticateResult.Fail("API Key tidak valid atau telah dinonaktifkan/kedaluwarsa.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, keyDto.Id.ToString()),
            new(ClaimTypes.Name, keyDto.Name),
            new("api_key_prefix", keyDto.KeyPrefix)
        };

        if (!string.IsNullOrWhiteSpace(keyDto.Scopes))
        {
            var scopes = keyDto.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var scope in scopes)
            {
                claims.Add(new Claim("scope", scope));
            }
        }

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}

