using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Wms.Web.Authorization;

/// <summary>
/// Menyediakan registrasi kebijakan otorisasi (policy) dinamis berbasis permission string.
/// Memungkinkan pemakaian [Authorize(Policy = Permissions.Xxx)] atau <AuthorizeView Policy="...">
/// tanpa perlu mendaftarkan setiap nama permission satu per satu secara manual.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        FallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        FallbackPolicyProvider.GetFallbackPolicyAsync();

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existingPolicy = await FallbackPolicyProvider.GetPolicyAsync(policyName);
        if (existingPolicy != null)
        {
            return existingPolicy;
        }

        // Buat policy dinamis dengan PermissionRequirement
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}
