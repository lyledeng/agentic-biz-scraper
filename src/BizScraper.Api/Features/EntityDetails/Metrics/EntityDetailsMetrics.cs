using System.Diagnostics.Metrics;

namespace BizScraper.Api.Features.EntityDetails.Metrics;

/// <summary>
/// Defines OpenTelemetry metrics for entity details scraping operations.
/// </summary>
public static class EntityDetailsMetrics
{
    public const string MeterName = "BizScraper.Api.EntityDetails";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("entity_details_requests_total");
    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>("entity_details_failures_total");
    private static readonly Counter<long> CertificateDownloadCounter = Meter.CreateCounter<long>("entity_details_certificate_downloads_total");
    private static readonly Histogram<double> DurationHistogram = Meter.CreateHistogram<double>("entity_details_duration_ms");
    private static readonly UpDownCounter<long> ActiveCounter = Meter.CreateUpDownCounter<long>("entity_details_active");

    public static void RequestStarted()
    {
        RequestCounter.Add(1);
        ActiveCounter.Add(1);
    }

    public static void RequestCompleted(double durationMs)
    {
        ActiveCounter.Add(-1);
        DurationHistogram.Record(durationMs);
    }

    public static void RequestFailed(double durationMs)
    {
        ActiveCounter.Add(-1);
        FailureCounter.Add(1);
        DurationHistogram.Record(durationMs);
    }

    public static void CertificateDownloaded() =>
        CertificateDownloadCounter.Add(1);

    private static readonly Counter<long> DocumentDownloadCounter = Meter.CreateCounter<long>("entity_details_document_downloads_total");

    public static void DocumentDownloaded() =>
        DocumentDownloadCounter.Add(1);
}
