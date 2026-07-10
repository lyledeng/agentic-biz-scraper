using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Actions;
using BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

using BizScraper.Api.Infrastructure.Scraping.Proxy;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Common.Extensions;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Features.Documents.Endpoints;
using BizScraper.Api.Features.Documents.Metrics;
using BizScraper.Api.Features.BusinessSearch.Metrics;
using BizScraper.Api.Features.EntityDetails.Metrics;
using BizScraper.Api.Features.ExecuteScript.Endpoints;
using BizScraper.Api.Features.Documents.Handlers;
using BizScraper.Api.Features.ExecuteScript.Handlers;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using BizScraper.Api.Features.ExecuteScript.Metrics;
using BizScraper.Api.Features.ExecuteScript.Validation;
using BizScraper.Api.Features.HealthCheck;
using BizScraper.Api.Infrastructure.CaptchaSolving;
using BizScraper.Api.Infrastructure.DocumentTranslation;
using BizScraper.Api.Infrastructure.Pdf;
using BizScraper.Api.Infrastructure.Persistence;
using BizScraper.Api.Infrastructure.Scraping;
using BizScraper.Api.Infrastructure.Storage;
using BizScraper.Api.Middleware;
using LiteBus.Commands;
using LiteBus.Extensions.Microsoft.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    options.UseUtcTimestamp = true;
});
builder.Logging.AddOpenTelemetry(otel =>
{
    otel.IncludeScopes = true;
    otel.IncludeFormattedMessage = true;
    otel.AddOtlpExporter();
});

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(8443, listenOptions => listenOptions.UseHttps());
});

builder.Services.AddOptions<PlaywrightOptions>()
    .Bind(builder.Configuration.GetSection("Playwright"))
    .Validate(
        options => string.IsNullOrWhiteSpace(options.BrowserEndpoint) || options.BrowserEndpoint.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) || options.BrowserEndpoint.StartsWith("ws://", StringComparison.OrdinalIgnoreCase),
        "Playwright:BrowserEndpoint must start with 'wss://' or 'ws://' when configured for Remote mode.")
    .ValidateOnStart();
builder.Services.AddOptions<FabAgentOptions>()
    .Bind(builder.Configuration.GetSection("FabAgent"));
builder.Services.AddOptions<FabDocumentTranslatorOptions>()
    .Bind(builder.Configuration.GetSection("FabDocumentTranslator"));
builder.Services.AddOptions<WindowsProxyOptions>()
    .Bind(builder.Configuration.GetSection("WindowsProxy"))
    .Validate(
        options => !options.IsConfigured || options.EndpointUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
        "WindowsProxy:EndpointUrl must start with 'https://' when configured.")
    .ValidateOnStart();
builder.Services.AddOptions<DiagnosticsOptions>()
    .Bind(builder.Configuration.GetSection("Playwright:Diagnostics"))
    .ValidateDataAnnotations()
    .Validate(options => Enum.IsDefined(options.TracingMode), "TracingMode must be a defined enum value.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.OutputPath), "OutputPath must not be empty.")
    .Validate(options => !options.CloudStorage.Enabled || !string.IsNullOrWhiteSpace(options.CloudStorage.ConnectionString), "CloudStorage:ConnectionString must not be empty when cloud upload is enabled.")
    .Validate(options => !options.CloudStorage.Enabled || !string.IsNullOrWhiteSpace(options.CloudStorage.ContainerName), "CloudStorage:ContainerName must not be empty when cloud upload is enabled.")
    .ValidateOnStart();

// Authentication & Authorization (T011, T013a)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"), jwtBearerScheme: JwtBearerDefaults.AuthenticationScheme, subscribeToJwtBearerMiddlewareDiagnosticsEvents: false);

builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Events ??= new JwtBearerEvents();

    options.Events.OnAuthenticationFailed = context =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var correlationId = context.HttpContext.TraceIdentifier;

        // Detect Entra ID connectivity failures and return 503 instead of 401
        if (context.Exception is HttpRequestException or SocketException)
        {
            logger.LogError(context.Exception, "Entra ID connectivity failure during token validation. CorrelationId={CorrelationId}", correlationId);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers["Retry-After"] = "30";
            context.Fail("Entra ID is temporarily unavailable.");
        }
        else
        {
            logger.LogWarning(context.Exception, "Authentication failed. CorrelationId={CorrelationId}", correlationId);
        }

        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BizScraperAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
                  context.User.HasClaim(c => c.Type == "http://schemas.microsoft.com/identity/claims/scope" && c.Value.Split(' ').Contains("access_as_user")) ||
                  context.User.HasClaim(c => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" && c.Value == "BizScraper.Execute") ||
                  context.User.IsInRole("BizScraper.Execute")));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BizScraper API",
        Version = "v1",
        Description = "API for scraping business entity search results across multiple states."
    });

    // Swagger OAuth2 Security (T016)
    var tenantId = builder.Configuration["AzureAd:TenantId"] ?? "<tenant-guid>";
    var clientId = builder.Configuration["AzureAd:ClientId"] ?? "<api-client-id>";
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize"),
                TokenUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token"),
                Scopes = new Dictionary<string, string>
                {
                    [$"api://{clientId}/access_as_user"] = "Access BizScraper API as the signed-in user"
                }
            }
        }
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
            },
            [$"api://{clientId}/access_as_user"]
        }
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<NullBlobStorageClient>();
builder.Services.AddSingleton<AzureBlobStorageClient>();
builder.Services.AddSingleton<IBlobStorageClient>(serviceProvider =>
{
    var diagnosticsOptions = serviceProvider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value;
    return diagnosticsOptions.CloudStorage.Enabled
        ? serviceProvider.GetRequiredService<AzureBlobStorageClient>()
        : serviceProvider.GetRequiredService<NullBlobStorageClient>();
});
builder.Services.AddSingleton<AuditTrailRepository>();
builder.Services.AddSingleton<ICaptchaSolver, FabCaptchaSolver>();
builder.Services.AddSingleton<IDocumentTranslator, FabDocumentTranslator>();
builder.Services.AddSingleton<MarkdownToPdfConverter>();
builder.Services.AddSingleton<IMarkdownToPdfConverter>(sp => sp.GetRequiredService<MarkdownToPdfConverter>());
builder.Services.AddSingleton<IPlaywrightPageFactory, PlaywrightPageFactory>();
builder.Services.AddSingleton<IHttpContextAccessorAccessor, HttpContextAccessorAdapter>();

// Generic Script Execution
builder.Services.AddSingleton<IWindowsProxyService, WindowsProxyService>();
builder.Services.AddSingleton<GenericScriptScraper>();
builder.Services.AddSingleton<ScriptExecutionAuditRepository>();
builder.Services.AddSingleton<ExecuteScriptValidator>();
builder.Services.AddSingleton(new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase));
builder.Services.AddSingleton<SearchResultMapper>();
builder.Services.AddSingleton<EntityDetailMapper>();
builder.Services.AddAllImplementations<ISearchResultMapper>();
builder.Services.AddAllImplementations<IEntityDetailMapper>();
builder.Services.AddSingleton<MapperRegistry>();
builder.Services.AddAllImplementations<IPostFlowDocumentProcessor>();
builder.Services.AddSingleton<PostFlowDocumentProcessorRegistry>();
builder.Services.AddScoped<ExecuteScriptHandler>();

// JSON Scraping Engine
builder.Services.AddSingleton<IAgentFallbackActionExecutor, NoOpAgentFallbackActionExecutor>();
builder.Services.AddSingleton<TargetResolver>();
builder.Services.AddSingleton<ConditionEvaluator>();
builder.Services.AddSingleton<OutputSchemaValidator>();
builder.Services.AddSingleton<ScrapingFlowEngine>();
builder.Services.AddSingleton<FlowDefinitionLoader>();
builder.Services.AddSingleton<IActionHandler, NavigateActionHandler>();
builder.Services.AddSingleton<IActionHandler, FillActionHandler>();
builder.Services.AddSingleton<IActionHandler, ClickActionHandler>();
builder.Services.AddSingleton<IActionHandler, WaitForLoadActionHandler>();
builder.Services.AddSingleton<IActionHandler, ExtractActionHandler>();
builder.Services.AddSingleton<IActionHandler, CheckTextActionHandler>();
builder.Services.AddSingleton<IActionHandler, ScreenshotActionHandler>();
builder.Services.AddSingleton<IActionHandler, LoopActionHandler>();
builder.Services.AddSingleton<IActionHandler, WaitForConditionActionHandler>();
builder.Services.AddSingleton<IActionHandler, CallServiceActionHandler>();
builder.Services.AddSingleton<IActionHandler, DownloadActionHandler>();

builder.Services.AddScoped<StreamDocumentHandler>();

builder.Services.AddLiteBus(liteBus =>
{
    liteBus.AddCommandModule(module => module.RegisterFromAssembly(typeof(ExecuteScriptHandler).Assembly));
});

var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(builder.Configuration["OpenTelemetry:ServiceName"] ?? "BizScraper.Api"));

openTelemetry.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddHttpClientInstrumentation();
    tracing.AddOtlpExporter();
});

openTelemetry.WithMetrics(metrics =>
{
    metrics.AddAspNetCoreInstrumentation();
    metrics.AddMeter(DocumentMetrics.MeterName);
    metrics.AddMeter(ExecuteScriptMetrics.MeterName);
    metrics.AddMeter(BusinessSearchMetrics.MeterName);
    metrics.AddMeter(EntityDetailsMetrics.MeterName);
    metrics.AddOtlpExporter();
});

builder.Services.AddCors(options =>
    options.AddPolicy("TestUiPolicy", policy =>
        policy.WithOrigins(builder.Configuration["Cors:TestUiOrigin"]!)
              .WithMethods("GET", "POST")
              .WithHeaders("Content-Type", "Accept", "Authorization")));

var app = builder.Build();

var playwrightOptions = app.Services.GetRequiredService<IOptions<PlaywrightOptions>>().Value;
if (playwrightOptions.IsRemoteMode)
{
    app.Logger.LogInformation("Browser mode (global default): Remote (endpoint: {Endpoint})", playwrightOptions.BrowserEndpoint);
}
else
{
    app.Logger.LogInformation("Browser mode (global default): Local (no BrowserEndpoint configured)");
}

var diagnosticsOptions = app.Services.GetRequiredService<IOptions<DiagnosticsOptions>>().Value;
if (diagnosticsOptions.CloudStorage.Enabled)
{
    await app.Services.GetRequiredService<AzureBlobStorageClient>().EnsureContainerExistsAsync(CancellationToken.None);
}

var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

app.UseSwagger(options =>
{
    options.PreSerializeFilters.Add((swagger, _) =>
    {
        swagger.Servers.Insert(0, new OpenApiServer { Url = pathBase ?? "/mvpoc/bizscraper-api" });
    });
});
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("./v1/swagger.json", "BizScraper API v1");
    options.RoutePrefix = "swagger";
    options.OAuthUsePkce();
});

// Load and validate JSON flow definitions at startup
var flowLoader = app.Services.GetRequiredService<FlowDefinitionLoader>();
var definitionsPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Scraping", "Definitions");
flowLoader.LoadAndValidateAll(definitionsPath, app.Logger);

// Log per-definition browser mode summary
{
    var localDefs = flowLoader.Definitions.Values
        .Where(d => d.Browser is not null && d.Browser.Mode.Equals("local", StringComparison.OrdinalIgnoreCase))
        .Select(d => d.Metadata.DefinitionSlug ?? d.Metadata.Id)
        .ToList();
    var remoteDefs = flowLoader.Definitions.Values
        .Where(d => d.Browser is not null && d.Browser.Mode.Equals("remote", StringComparison.OrdinalIgnoreCase))
        .Select(d => d.Metadata.DefinitionSlug ?? d.Metadata.Id)
        .ToList();
    var defaultDefs = flowLoader.Definitions.Values
        .Where(d => d.Browser is null)
        .Select(d => d.Metadata.DefinitionSlug ?? d.Metadata.Id)
        .ToList();
    var windowsDefs = flowLoader.Definitions.Values
        .Where(d => d.Browser is not null && d.Browser.Mode.Equals("windows", StringComparison.OrdinalIgnoreCase))
        .Select(d => d.Metadata.DefinitionSlug ?? d.Metadata.Id)
        .ToList();

    app.Logger.LogInformation(
        "Per-definition browser modes: {LocalCount} local [{LocalSlugs}], {RemoteCount} remote [{RemoteSlugs}], {WindowsCount} windows [{WindowsSlugs}], {DefaultCount} global default [{DefaultSlugs}]",
        localDefs.Count, string.Join(", ", localDefs),
        remoteDefs.Count, string.Join(", ", remoteDefs),
        windowsDefs.Count, string.Join(", ", windowsDefs),
        defaultDefs.Count, string.Join(", ", defaultDefs));

    var windowsProxyOptions = app.Services.GetRequiredService<IOptions<WindowsProxyOptions>>().Value;
    if (windowsDefs.Count > 0 && !windowsProxyOptions.IsConfigured)
    {
        app.Logger.LogWarning(
            "{WindowsCount} definitions require Windows proxy mode but no WindowsProxy:EndpointUrl is configured — they will fall back to local browser.",
            windowsDefs.Count);
    }
    else if (windowsDefs.Count > 0)
    {
        app.Logger.LogInformation(
            "Windows proxy configured: {Endpoint} (timeout={TimeoutSeconds}s) for {Count} definitions.",
            windowsProxyOptions.EndpointUrl, windowsProxyOptions.TimeoutSeconds, windowsDefs.Count);
    }
}

app.UseExceptionHandler();
app.UseCors("TestUiPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestTimeoutMiddleware>();

var protectedGroup = app.MapGroup(string.Empty).RequireAuthorization("BizScraperAccess");
protectedGroup.MapExecuteScriptEndpoints();
protectedGroup.MapDocumentEndpoints();

var publicGroup = app.MapGroup(string.Empty).AllowAnonymous();
publicGroup.MapHealthCheckEndpoints();

app.MapGet("/", () => Results.Redirect("/healthz")).AllowAnonymous();

await app.RunAsync();

public partial class Program;
