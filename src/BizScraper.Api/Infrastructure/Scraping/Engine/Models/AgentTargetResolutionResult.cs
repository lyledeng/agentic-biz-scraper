namespace BizScraper.Api.Infrastructure.Scraping.Engine.Models;

public enum AgentTargetResolutionAction
{
    Click,
    Fill
}

public sealed record AgentTargetResolutionResult(
    bool Success,
    string? ResolvedSelector,
    string? ResolvedXPath,
    string? ResolvedRole,
    string? ResolvedName,
    AgentTargetResolutionAction Action,
    string? Message);
