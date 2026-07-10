using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BizScraper.IntegrationTests;

/// <summary>
/// Adds a test authentication scheme that auto-succeeds with a delegated user token,
/// allowing existing integration tests to pass through authentication without changes.
/// </summary>
internal static class TestAuthExtensions
{
    /// <summary>Default claims simulating a delegated user with the access_as_user scope.</summary>
    internal static readonly Claim[] DefaultDelegatedClaims =
    [
        new("preferred_username", "test@contoso.com"),
        new("http://schemas.microsoft.com/identity/claims/scope", "access_as_user")
    ];

    /// <summary>
    /// Registers a test authentication scheme that automatically authenticates with <see cref="DefaultDelegatedClaims"/>.
    /// </summary>
    internal static IServiceCollection AddTestAuthentication(this IServiceCollection services) =>
        AddTestAuthentication(services, DefaultDelegatedClaims);

    /// <summary>
    /// Registers a test authentication scheme that automatically authenticates with the specified claims.
    /// </summary>
    internal static IServiceCollection AddTestAuthentication(this IServiceCollection services, Claim[] claims)
    {
        services.AddAuthentication("TestScheme")
            .AddScheme<AuthenticationSchemeOptions, AutoSuccessAuthHandler>("TestScheme", _ => { });
        services.Configure<AutoSuccessAuthHandlerOptions>(opts => opts.Claims = claims);
        return services;
    }
}

#pragma warning disable CA1812
internal sealed class AutoSuccessAuthHandlerOptions
{
    public Claim[] Claims { get; set; } = TestAuthExtensions.DefaultDelegatedClaims;
}

internal sealed class AutoSuccessAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AutoSuccessAuthHandlerOptions> testOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(testOptions.Value.Claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
