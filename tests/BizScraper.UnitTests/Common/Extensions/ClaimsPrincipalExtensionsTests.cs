using System.Security.Claims;
using BizScraper.Api.Common.Extensions;

namespace BizScraper.UnitTests.Common.Extensions;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetCallerIdentity_ReturnPreferredUsername_ForDelegatedToken()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("preferred_username", "user@contoso.com"),
            new Claim("http://schemas.microsoft.com/identity/claims/scope", "access_as_user")
        ], "Bearer"));

        var result = principal.GetCallerIdentity();

        Assert.Equal("user@contoso.com", result);
    }

    [Fact]
    public void GetCallerIdentity_ReturnsAzp_ForApplicationToken()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("azp", "a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "BizScraper.Execute")
        ], "Bearer"));

        var result = principal.GetCallerIdentity();

        Assert.Equal("a1b2c3d4-e5f6-7890-abcd-ef1234567890", result);
    }

    [Fact]
    public void GetCallerIdentity_ReturnsNull_ForAnonymous()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = principal.GetCallerIdentity();

        Assert.Null(result);
    }

    [Fact]
    public void GetCallerIdentity_PrefersPreferredUsername_WhenBothPresent()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("preferred_username", "user@contoso.com"),
            new Claim("azp", "some-app-id")
        ], "Bearer"));

        var result = principal.GetCallerIdentity();

        Assert.Equal("user@contoso.com", result);
    }
}
