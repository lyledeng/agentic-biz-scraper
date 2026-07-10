namespace BizScraper.Api.Infrastructure.Scraping.Engine.Models;

public sealed record AgentFallbackActionExecutionResult(
    bool Success,
    string? Message);
