using System.Diagnostics.Metrics;

namespace BizScraper.Api.Features.BusinessSearch.Metrics;

/// <summary>
/// Defines OpenTelemetry metrics for business search operations.
/// </summary>
public static class BusinessSearchMetrics
{
    public const string MeterName = "BizScraper.Api.BusinessSearch";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> SearchRequestCounter = Meter.CreateCounter<long>("business_search_requests_total");
    private static readonly Counter<long> SearchFailureCounter = Meter.CreateCounter<long>("business_search_failures_total");
    private static readonly Histogram<double> SearchDurationHistogram = Meter.CreateHistogram<double>("business_search_duration_ms");
    private static readonly UpDownCounter<long> ActiveSearchCounter = Meter.CreateUpDownCounter<long>("business_search_active");
    private static readonly Histogram<long> PagesScrapedHistogram = Meter.CreateHistogram<long>("business_search_pages_scraped");
    private static readonly Counter<long> CaptchaSolvesCounter = Meter.CreateCounter<long>("captcha_solves_total");
    private static readonly Counter<long> CaptchaFailuresCounter = Meter.CreateCounter<long>("captcha_failures_total");

    public static void RequestStarted()
    {
        SearchRequestCounter.Add(1);
        ActiveSearchCounter.Add(1);
    }

    public static void RequestCompleted(double durationMs, int pagesScraped)
    {
        ActiveSearchCounter.Add(-1);
        SearchDurationHistogram.Record(durationMs);
        PagesScrapedHistogram.Record(pagesScraped);
    }

    public static void RequestFailed(double durationMs)
    {
        ActiveSearchCounter.Add(-1);
        SearchFailureCounter.Add(1);
        SearchDurationHistogram.Record(durationMs);
    }

    public static void CaptchaSolved() => CaptchaSolvesCounter.Add(1);

    public static void CaptchaFailed() => CaptchaFailuresCounter.Add(1);
}
