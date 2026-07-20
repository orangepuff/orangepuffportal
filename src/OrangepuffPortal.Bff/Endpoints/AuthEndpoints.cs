using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using OrangepuffPortal.Bff.Infrastructure.IdentityGateway;
using System.Security.Claims;

namespace OrangepuffPortal.Bff.Endpoints
{
    /// <summary>
    /// Maps /bff/login, /bff/logout, /bff/me, /bff/me/permissions.
    /// </summary>
    public static class AuthEndpoints
    {
        public static void MapAuthEndpoints(this WebApplication app)
        {
            app.MapGet("/bff/login", (string? returnUrl, IConfiguration configuration) =>
            {
                var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? throw new InvalidOperationException("Frontend:BaseUrl is not configured.");

                var safeReturnUrl = IsSafeReturnUrl(returnUrl) ? returnUrl : "/";

                var properties = new AuthenticationProperties
                {
                    RedirectUri = $"{frontendBaseUrl}{safeReturnUrl}",
                    // Without this the cookie is session-only — it survives sliding
                    // revalidation but is dropped the moment the browser closes, even
                    // though ExpireTimeSpan says 8 hours.
                    IsPersistent = true,
                };

                return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
            });

            app.MapPost("/bff/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.NoContent();
            }).RequireAuthorization();

            app.MapGet("/bff/me", async (ClaimsPrincipal user, IIdentityGateway client, CancellationToken ct) =>
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var email = user.FindFirst(ClaimTypes.Email)?.Value;
                var displayName = user.FindFirst(ClaimTypes.Name)?.Value;
                var isAdmin = await client.IsUserAdminAsync(int.Parse(userId), ct);

                return Results.Ok(new MeResponse(userId, email, displayName, isAdmin));
            }).RequireAuthorization();

            app.MapGet("/bff/me/permissions", async (ClaimsPrincipal user, IIdentityGateway client, CancellationToken ct) =>
            {
                var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var permissions = await client.GetEffectivePermissionsAsync(userId, ct);
                return Results.Ok(permissions);
            }).RequireAuthorization();
        }

        private static bool IsSafeReturnUrl(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) &&
            returnUrl.StartsWith('/') &&
            !returnUrl.StartsWith("//") &&
            !returnUrl.StartsWith("/\\");
    }
}
