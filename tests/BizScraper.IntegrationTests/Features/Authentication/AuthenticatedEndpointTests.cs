using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BizScraper.IntegrationTests.Features.Authentication;

/// <summary>
/// Integration tests verifying authentication and authorization on protected and anonymous endpoints.
/// Uses a test authentication handler to simulate delegated and application tokens without hitting Entra ID.
/// </summary>
public sealed class AuthenticatedEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthenticatedEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/v2/definitions")]
    public async Task ProtectedEndpoint_Returns401_WhenNoToken(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/healthz")]
    public async Task AnonymousEndpoint_Returns200_WithoutToken(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyEndpoint_ReturnsSuccessOrUnavailable_WithoutToken()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/ready");

        // /ready probes browser connectivity — may return 503 when no browser is available in CI
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected OK or ServiceUnavailable but got {response.StatusCode}");
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns200_WithValidDelegatedToken()
    {
        var client = CreateClientWithTestAuth(
        [
            new Claim("preferred_username", "user@contoso.com"),
            new Claim("http://schemas.microsoft.com/identity/claims/scope", "access_as_user")
        ]);

        var response = await client.GetAsync("/api/v2/definitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns200_WithValidApplicationToken()
    {
        var client = CreateClientWithTestAuth(
        [
            new Claim("azp", "test-system-client-id"),
            new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "BizScraper.Execute")
        ]);

        var response = await client.GetAsync("/api/v2/definitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns403_WithValidTokenButWrongScope()
    {
        var client = CreateClientWithTestAuth(
        [
            new Claim("preferred_username", "user@contoso.com"),
            new Claim("http://schemas.microsoft.com/identity/claims/scope", "wrong_scope")
        ]);

        var response = await client.GetAsync("/api/v2/definitions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateClientWithTestAuth(Claim[] claims)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
                services.Configure<TestAuthHandlerOptions>(opts => opts.Claims = claims);
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme", "test-token");
        return client;
    }
}

#pragma warning disable CA1812 // Both classes instantiated via DI/reflection by the authentication framework
internal sealed class TestAuthHandlerOptions
{
    public Claim[] Claims { get; set; } = [];
}

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<TestAuthHandlerOptions> testOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = testOptions.Value.Claims;
        if (claims.Length == 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("No claims configured."));
        }

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
