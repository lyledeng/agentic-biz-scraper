using System.Diagnostics.Metrics;

namespace BizScraper.Api.Features.Documents.Metrics;

/// <summary>
/// Defines OpenTelemetry metrics for document streaming operations.
/// </summary>
public static class DocumentMetrics
{
    public const string MeterName = "BizScraper.Api.Documents";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("bizscraper.documents.requests");
    private static readonly Histogram<double> DurationHistogram = Meter.CreateHistogram<double>("bizscraper.documents.duration_ms");

    public static void RecordRequest(int statusCode, double durationMs)
    {
        RequestCounter.Add(1, new KeyValuePair<string, object?>("status", statusCode));
        DurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("status", statusCode));
    }
}
