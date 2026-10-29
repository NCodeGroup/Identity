#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.Bff.Antiforgery;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Antiforgery;

public class BffMiddlewareTests : BaseTests
{
    private Mock<IBffAuditRecorder> MockRecorder { get; }
    private BffOptions Options { get; } = new();

    public BffMiddlewareTests()
    {
        MockRecorder = CreateStrictMock<IBffAuditRecorder>();
    }

    private BffMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, Microsoft.Extensions.Options.Options.Create(Options), MockRecorder.Object);

    private static DefaultHttpContext CreateContext(bool guarded)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        if (guarded)
        {
            httpContext.SetEndpoint(
                new Endpoint(null, new EndpointMetadataCollection(new BffApiAttribute()), "guarded")
            );
        }
        return httpContext;
    }

    private static JsonElement ReadProblem(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(httpContext.Response.Body);
        return document.RootElement.Clone();
    }

    private sealed class FakeAuthenticationFeature : IAuthenticationFeature
    {
        public PathString OriginalPath { get; set; }
        public PathString OriginalPathBase { get; set; }
    }

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_AfterAuthentication_Throws()
    {
        var httpContext = CreateContext(guarded: false);
        httpContext.Features.Set<IAuthenticationFeature>(new FakeAuthenticationFeature());
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(httpContext)
        );
    }

    [Fact]
    public async Task InvokeAsync_GuardedWithoutHeader_Returns403AndSkipsNext()
    {
        var httpContext = CreateContext(guarded: true);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        MockRecorder
            .Setup(x =>
                x.AntiforgeryRejectedAsync(
                    httpContext,
                    BffRequestGuard.AntiforgeryHeaderMissing,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await middleware.InvokeAsync(httpContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(
            BffProblemTypes.AntiforgeryHeaderMissing,
            ReadProblem(httpContext).GetProperty("type").GetString()
        );
    }

    [Fact]
    public async Task InvokeAsync_GuardedCrossSite_Returns403CrossSite()
    {
        var httpContext = CreateContext(guarded: true);
        httpContext.Request.Headers[Options.Antiforgery.HeaderName] = Options
            .Antiforgery
            .HeaderValue;
        httpContext.Request.Headers["Sec-Fetch-Site"] = "cross-site";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        MockRecorder
            .Setup(x =>
                x.AntiforgeryRejectedAsync(
                    httpContext,
                    BffRequestGuard.CrossSiteRequest,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(
            BffProblemTypes.CrossSiteRequest,
            ReadProblem(httpContext).GetProperty("type").GetString()
        );
    }

    [Fact]
    public async Task InvokeAsync_GuardedWithHeader_CallsNextAndSetsFeature()
    {
        var httpContext = CreateContext(guarded: true);
        httpContext.Request.Headers[Options.Antiforgery.HeaderName] = Options
            .Antiforgery
            .HeaderValue;
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
        var feature = httpContext.Features.Get<BffRequestFeature>();
        Assert.NotNull(feature);
        Assert.Same(httpContext.GetEndpoint(), feature.CheckedEndpoint);
    }

    [Fact]
    public async Task InvokeAsync_UnguardedEndpoint_CallsNextWithoutCheckedEndpoint()
    {
        var httpContext = CreateContext(guarded: false);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
        Assert.Null(httpContext.Features.Get<BffRequestFeature>()?.CheckedEndpoint);
    }

    [Fact]
    public async Task InvokeAsync_StoreUnavailable_Returns503()
    {
        var httpContext = CreateContext(guarded: false);
        var middleware = CreateMiddleware(_ =>
            throw new BffStoreUnavailableException("down", new InvalidOperationException())
        );

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpContext.Response.StatusCode);
        Assert.Equal(
            BffProblemTypes.StoreUnavailable,
            ReadProblem(httpContext).GetProperty("type").GetString()
        );
    }

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized, BffProblemTypes.SessionRequired)]
    [InlineData(StatusCodes.Status502BadGateway, BffProblemTypes.TokenUnavailable)]
    public async Task InvokeAsync_TokenUnavailable_ReturnsProblemWithStatus(
        int status,
        string expectedType
    )
    {
        var httpContext = CreateContext(guarded: false);
        var middleware = CreateMiddleware(_ =>
            throw new BffTokenUnavailableException(status, BffTokenFailureReasons.SessionRequired)
        );

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(status, httpContext.Response.StatusCode);
        Assert.Equal(expectedType, ReadProblem(httpContext).GetProperty("type").GetString());
    }

    #endregion
}
