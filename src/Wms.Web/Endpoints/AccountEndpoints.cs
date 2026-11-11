using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Wms.Domain.Entities.Identity;

namespace Wms.Web.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/account");

        group.MapPost("/login", async (
            HttpContext context,
            [FromForm] string emailOrUsername,
            [FromForm] string password,
            [FromForm] bool? rememberMe,
            [FromForm] string? returnUrl,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager) =>
        {
            if (string.IsNullOrWhiteSpace(emailOrUsername) || string.IsNullOrWhiteSpace(password))
            {
                return Results.Redirect($"/login?error=invalid_credentials&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
            }

            var user = await userManager.FindByEmailAsync(emailOrUsername.Trim())
                       ?? await userManager.FindByNameAsync(emailOrUsername.Trim());

            if (user == null || !user.IsActive)
            {
                return Results.Redirect($"/login?error=invalid_credentials&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
            }

            var result = await signInManager.PasswordSignInAsync(user, password, rememberMe ?? false, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                return Results.Redirect($"/login?error=locked_out&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
            }

            if (!result.Succeeded)
            {
                return Results.Redirect($"/login?error=invalid_credentials&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
            }

            return Results.LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
        }).RequireRateLimiting("login-limiter");

        group.MapPost("/logout", async (
            SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/login");
        });

        return endpoints;
    }
}
