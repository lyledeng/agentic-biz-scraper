using System.Security.Claims;

namespace BizScraper.Api.Common.Extensions;

/// <summary>
/// Extension methods for extracting caller identity from JWT claims.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the caller identity from the authenticated principal.
    /// Returns <c>preferred_username</c> for delegated (user) tokens,
    /// <c>azp</c> (authorized party / client ID) for application tokens,
    /// or <c>null</c> for anonymous/unauthenticated requests.
    /// </summary>
    public static string? GetCallerIdentity(this ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Delegated tokens carry preferred_username (the user's UPN)
        var preferredUsername = principal.FindFirstValue("preferred_username");
        if (!string.IsNullOrEmpty(preferredUsername))
        {
            return preferredUsername;
        }

        // Application tokens carry azp (authorized party — the calling app's client ID)
        var azp = principal.FindFirstValue("azp");
        return string.IsNullOrEmpty(azp) ? null : azp;
    }
}
