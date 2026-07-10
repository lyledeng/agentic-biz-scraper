using System.Security.Claims;
using BizScraper.Api.Common.Extensions;

namespace BizScraper.UnitTests.Features.Authentication;

/// <summary>
/// Verifies that CallerIdentity extraction integrates correctly with ClaimsPrincipal
/// from typical Entra ID token scenarios, covering the wiring path used by ExecuteScriptHandler.
/// </summary>
public sealed class CallerIdentityWiringTests
{
    [Fact]
    public void DelegatedToken_ExtractsUpn_AsCallerIdentity()
    {
        // Simulate a delegated (user) token with preferred_username and scope
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("preferred_username", "alice@contoso.com"),
            new Claim("http://schemas.microsoft.com/identity/claims/scope", "access_as_user"),
            new Claim("aud", "api://test-api-client-id"),
            new Claim("tid", "test-tenant-id")
        ], "Bearer"));

        var callerIdentity = principal.GetCallerIdentity();

        Assert.Equal("alice@contoso.com", callerIdentity);
    }

    [Fact]
    public void ApplicationToken_ExtractsClientId_AsCallerIdentity()
    {
        // Simulate an application (client credentials) token with azp and roles
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("azp", "b2c3d4e5-f6a7-8901-bcde-f23456789012"),
            new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "BizScraper.Execute"),
            new Claim("aud", "api://test-api-client-id"),
            new Claim("tid", "test-tenant-id")
        ], "Bearer"));

        var callerIdentity = principal.GetCallerIdentity();

        Assert.Equal("b2c3d4e5-f6a7-8901-bcde-f23456789012", callerIdentity);
    }

    [Fact]
    public void UnauthenticatedRequest_ReturnsNull_CallerIdentity()
    {
        // Anonymous (no auth) request
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var callerIdentity = principal.GetCallerIdentity();

        Assert.Null(callerIdentity);
    }

    [Fact]
    public void NullPrincipal_ReturnsNull_CallerIdentity()
    {
        ClaimsPrincipal? principal = null;

        var callerIdentity = principal.GetCallerIdentity();

        Assert.Null(callerIdentity);
    }
}
