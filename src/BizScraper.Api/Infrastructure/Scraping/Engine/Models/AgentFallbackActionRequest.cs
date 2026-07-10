namespace BizScraper.Api.Infrastructure.Scraping.Engine.Models;

public abstract record AgentFallbackActionRequest(
    BrowserObservation Observation,
    string? TargetDescription);

public sealed record ClickActionRequest(
    BrowserObservation Observation,
    string? TargetDescription,
    string? Selector) : AgentFallbackActionRequest(Observation, TargetDescription);

public sealed record FillActionRequest(
    BrowserObservation Observation,
    string? TargetDescription,
    string? Value,
    string? Selector) : AgentFallbackActionRequest(Observation, TargetDescription);
