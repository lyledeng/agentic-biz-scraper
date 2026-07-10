using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class CallServiceActionHandler : IActionHandler
{
    private readonly Dictionary<string, Func<ActionContext, FlowActionV2, CancellationToken, Task<object?>>> _services;

    public CallServiceActionHandler(ICaptchaSolver captchaSolver)
    {
        _services = new(StringComparer.OrdinalIgnoreCase)
        {
            ["captcha-solver"] = async (context, action, ct) =>
            {
                var inputVar = action.InputVariable ?? "captchaImageBytes";
                if (!context.Variables.TryGetValue(inputVar, out var imageObj) || imageObj is null)
                {
                    throw new InvalidOperationException(
                        $"captcha-solver requires '{inputVar}' variable to contain the CAPTCHA image.");
                }

                byte[] imageBytes = imageObj switch
                {
                    byte[] bytes => bytes,
                    string dataUrl when dataUrl.Contains(',', StringComparison.Ordinal) =>
                        Convert.FromBase64String(dataUrl[(dataUrl.IndexOf(',', StringComparison.Ordinal) + 1)..]),
                    string base64 => Convert.FromBase64String(base64),
                    _ => throw new InvalidOperationException(
                        $"'{inputVar}' must be byte[] or a base64/data-URL string.")
                };

                return await captchaSolver.SolveAsync(imageBytes, ct);
            }
        };
    }

    public string ActionType => "call-service";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action) =>
        $"service={action.ServiceName}, input={action.InputVariable}, output={action.OutputVariable}";

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        var serviceName = action.ServiceName
            ?? throw new InvalidOperationException("call-service action requires 'serviceName'.");

        if (!_services.TryGetValue(serviceName, out var serviceFunc))
        {
            throw new InvalidOperationException($"No service registered with name '{serviceName}'.");
        }

        var result = await serviceFunc(context, action, cancellationToken);

        if (!string.IsNullOrEmpty(action.OutputVariable))
        {
            context.Variables[action.OutputVariable] = result;
        }
    }
}
