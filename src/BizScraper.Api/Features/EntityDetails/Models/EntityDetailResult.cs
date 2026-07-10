namespace BizScraper.Api.Features.EntityDetails.Models;

/// <summary>
/// Core entity information scraped from a Secretary of State details page.
/// </summary>
public sealed record EntityDetailResult(
    string Name,
    string Status,
    string FormationDate,
    string IdNumber,
    string Form,
    string? PeriodicReportMonth,
    string Jurisdiction,
    string? PrincipalOfficeStreetAddress,
    string? PrincipalOfficeMailingAddress,
    string? SubStatus = null,
    string? StandingTax = null,
    string? StandingRA = null,
    string? StandingOther = null,
    string? InactiveDate = null,
    string? TermOfDuration = null,
    string? FormedIn = null,
    string? LatestAnnualReportYear = null,
    string? AnnualReportExempt = null,
    string? LicenseTaxPaid = null);
