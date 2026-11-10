// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Endpoints;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class BffUserHandlerTests : BaseTests
{
    private BffUserHandler CreateHandler(HttpContext httpContext, JsonElement? document)
    {
        var factory = CreateStrictMock<IBffUserDocumentFactory>();
        factory
            .Setup(x => x.CreateAsync(httpContext, httpContext.RequestAborted))
            .ReturnsAsync(document)
            .Verifiable();
        return new BffUserHandler(factory.Object);
    }

    private static async Task<string> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, SecureEncoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_NoSession_WritesUnauthorizedProblem()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var handler = CreateHandler(httpContext, null);

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal(
            "no-store, no-cache, max-age=0",
            httpContext.Response.Headers.CacheControl.ToString()
        );
        var body = await ReadBodyAsync(httpContext);
        Assert.Contains(BffProblemTypes.SessionRequired, body);
    }

    [Fact]
    public async Task HandleAsync_WithDocument_WritesTheDocumentUncached()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var document = JsonElement.Parse("""{"sub":"sub-1","claims":{"role":["admin","user"]}}""");
        var handler = CreateHandler(httpContext, document);

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", httpContext.Response.ContentType);
        Assert.Equal(
            "no-store, no-cache, max-age=0",
            httpContext.Response.Headers.CacheControl.ToString()
        );
        var body = await ReadBodyAsync(httpContext);
        Assert.Equal(document.GetRawText(), JsonElement.Parse(body).GetRawText());
    }

    #endregion
}
