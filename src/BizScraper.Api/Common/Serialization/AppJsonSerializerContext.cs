using System.Text.Json.Serialization;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Features.BusinessSearch.Models;
using BizScraper.Api.Features.EntityDetails.Models;
using BizScraper.Api.Infrastructure.CaptchaSolving;

namespace BizScraper.Api.Common.Serialization;

[JsonSerializable(typeof(BusinessEntityResult))]
[JsonSerializable(typeof(List<BusinessEntityResult>))]
[JsonSerializable(typeof(IReadOnlyList<BusinessEntityResult>))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(SearchScrapeResult))]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(NormalizedSearchResult))]
[JsonSerializable(typeof(NormalizedSearchScrapeResult))]
[JsonSerializable(typeof(NormalizedSearchResponse))]
[JsonSerializable(typeof(FabCaptchaSolverRequest))]
[JsonSerializable(typeof(FabCaptchaSolverResponse))]
[JsonSerializable(typeof(EntityDetailResponse))]
[JsonSerializable(typeof(EntityDetailResult))]
[JsonSerializable(typeof(RegisteredAgentResult))]
[JsonSerializable(typeof(CertificateResult))]
[JsonSerializable(typeof(PartyResult))]
[JsonSerializable(typeof(IReadOnlyList<PartyResult>))]
[JsonSerializable(typeof(HistoryDocumentResult))]
[JsonSerializable(typeof(IReadOnlyList<HistoryDocumentResult>))]
// Unified entity schema (v2)
[JsonSerializable(typeof(UnifiedSearchResult))]
[JsonSerializable(typeof(UnifiedSearchResult[]))]
[JsonSerializable(typeof(UnifiedEntityDetailResponse))]
[JsonSerializable(typeof(DetailSection))]
[JsonSerializable(typeof(AgentSection))]
[JsonSerializable(typeof(CertificateSection))]
[JsonSerializable(typeof(PartyEntry))]
[JsonSerializable(typeof(IReadOnlyList<PartyEntry>))]
[JsonSerializable(typeof(DocumentEntry))]
[JsonSerializable(typeof(IReadOnlyList<DocumentEntry>))]
[JsonSerializable(typeof(DownloadReference))]
[JsonSerializable(typeof(IReadOnlyList<DownloadReference>))]
[JsonSerializable(typeof(IowaNameEntry))]
[JsonSerializable(typeof(IReadOnlyList<IowaNameEntry>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed partial class AppJsonSerializerContext : JsonSerializerContext;