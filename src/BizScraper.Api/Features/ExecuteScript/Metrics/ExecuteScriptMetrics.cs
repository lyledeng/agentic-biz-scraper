using System.Diagnostics.Metrics;

namespace BizScraper.Api.Features.ExecuteScript.Metrics;

/// <summary>
/// Defines OpenTelemetry metrics for generic script execution operations.
/// </summary>
public static class ExecuteScriptMetrics
{
    public const string MeterName = "BizScraper.Api.ExecuteScript";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("execute_script_requests_total");
    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>("execute_script_failures_total");
    private static readonly Histogram<double> DurationHistogram = Meter.CreateHistogram<double>("execute_script_duration_ms");
    private static readonly UpDownCounter<long> ActiveCounter = Meter.CreateUpDownCounter<long>("execute_script_active");

    public static void RequestStarted(string definitionSlug)
    {
        RequestCounter.Add(1, new KeyValuePair<string, object?>("definition", definitionSlug));
        ActiveCounter.Add(1, new KeyValuePair<string, object?>("definition", definitionSlug));
    }

    public static void RequestCompleted(string definitionSlug, double durationMs)
    {
        ActiveCounter.Add(-1, new KeyValuePair<string, object?>("definition", definitionSlug));
        DurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("definition", definitionSlug));
    }

    public static void RequestFailed(string definitionSlug, double durationMs)
    {
        ActiveCounter.Add(-1, new KeyValuePair<string, object?>("definition", definitionSlug));
        FailureCounter.Add(1, new KeyValuePair<string, object?>("definition", definitionSlug));
        DurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("definition", definitionSlug));
    }
}
