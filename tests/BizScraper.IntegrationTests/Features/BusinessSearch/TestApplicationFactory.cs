using BizScraper.Api.Infrastructure.Scraping.Engine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BizScraper.IntegrationTests.Features.BusinessSearch;

/// <summary>
/// V2 test factory that replaces GenericScriptScraper with a provided substitute.
/// </summary>
internal sealed class V2BusinessSearchTestFactory(GenericScriptScraper scraper) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<GenericScriptScraper>();
            services.AddSingleton(scraper);
            services.AddTestAuthentication();
        });
    }
}
