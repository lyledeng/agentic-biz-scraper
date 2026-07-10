namespace BizScraper.Api.Common.Interfaces;

/// <summary>
/// Provides access to the current <see cref="HttpContext"/> for audit trail correlation.
/// </summary>
public interface IHttpContextAccessorAccessor
{
    HttpContext? HttpContext { get; }
}

/// <summary>
/// Adapts <see cref="IHttpContextAccessor"/> to <see cref="IHttpContextAccessorAccessor"/>.
/// </summary>
public sealed class HttpContextAccessorAdapter(IHttpContextAccessor httpContextAccessor) : IHttpContextAccessorAccessor
{
    public HttpContext? HttpContext => httpContextAccessor.HttpContext;
}
