using System.Text;
using BizScraper.Api.Features.BusinessSearch.Exceptions;
using BizScraper.Api.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BizScraper.UnitTests.Middleware;

public sealed class GlobalExceptionHandlerTests
{
    private static GlobalExceptionHandler CreateHandler()
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Production);
        return new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, env);
    }

    [Fact]
    public async Task TryHandleAsync_MapsBusyExceptionTo503()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, new ServiceBusyException("Busy", 30), CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Contains("service-busy", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryHandleAsync_MapsExceededRecordCountTo422()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            new ExceededRecordCountException("Too many results"),
            CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        Assert.Contains("exceeded-record-count", payload, StringComparison.OrdinalIgnoreCase);
    }
}
