using BizScraper.Api.Features.ExecuteScript.Models;
using LiteBus.Commands.Abstractions;

namespace BizScraper.Api.Features.ExecuteScript.Commands;

/// <summary>
/// CQRS command to execute a named scraping flow definition with runtime parameters.
/// </summary>
public sealed record ExecuteScriptCommand(
    string Definition,
    Dictionary<string, object?>? Parameters,
    string CorrelationId) : ICommand<ExecuteScriptResponse>;
