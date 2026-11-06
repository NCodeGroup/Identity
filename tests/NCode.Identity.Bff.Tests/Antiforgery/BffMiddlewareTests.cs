// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Antiforgery;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Antiforgery;

public class BffMiddlewareTests : BaseTests
{
    private Mock<IBffAuditRecorder> MockRecorder { get; }
    private Mock<BffSessionResolver> MockResolver { get; }
    private Mock<IAntiforgery> MockAntiforgery { get; }
    private BffOptions Options { get; } = new();

    public BffMiddlewareTests()
    {
        MockRecorder = CreateStrictMock<IBffAuditRecorder>();
        MockAntiforgery = CreateStrictMock<IAntiforgery>();

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 2, 5, 0, 0, TimeSpan.Zero)
        );
        MockResolver = CreateStrictMock<BffSessionResolver>(
            CreateStrictMock<IBffSessionManager>().Object,
            new BffCookies(timeProvider),
            Array.Empty<IBffSessionValidator>(),
            BffTestFrontends.CreateCatalog(),
            timeProvider
        );
    }

    private BffMiddleware CreateMiddleware(
        RequestDelegate next,
        BffFrontendCatalog? catalog = null
    ) =>
        new(
            next,
            Microsoft.Extensions.Options.Options.Create(Options),
            catalog ?? BffTestFrontends.CreateCatalog(),
            MockResolver.Object,
            MockAntiforgery.Object,
            MockRecorder.Object
        );

    private static DefaultHttpContext CreatePageContext(string method)
    {
        var httpContext = CreateContext(guarded: false);
        httpContext.Request.Method = method;
        httpContext.SetEndpoint(
            new Endpoint(null, new EndpointMetadataCollection(new BffPageAttribute()), "page")
        );
        return httpContext;
    }

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
        return JsonSerializer.Deserialize<JsonElement>(httpContext.Response.Body);
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
    public async Task InvokeAsync_SameSiteRequestFromTrustedOrigin_CallsNext()
    {
        const string origin = "https://admin.example.com";
        var httpContext = CreateContext(guarded: true);
        httpContext.Request.Headers[Options.Antiforgery.HeaderName] = Options
            .Antiforgery
            .HeaderValue;
        httpContext.Request.Headers["Sec-Fetch-Site"] = "same-site";
        httpContext.Request.Headers.Origin = origin;
        var nextCalled = false;
        var middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            BffTestFrontends.CreateCatalog(x => x.TrustedOrigins = [origin])
        );

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_SameSiteRequestWithoutAFrontend_IsRefused()
    {
        const string origin = "https://admin.example.com";
        var httpContext = CreateContext(guarded: true);
        httpContext.Request.Path = "/api";
        httpContext.Request.Headers[Options.Antiforgery.HeaderName] = Options
            .Antiforgery
            .HeaderValue;
        httpContext.Request.Headers["Sec-Fetch-Site"] = "same-site";
        httpContext.Request.Headers.Origin = origin;
        var catalog = BffTestFrontends.CreateCatalog(x =>
        {
            x.TrustedOrigins = [origin];
            x.Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Path,
                Values = ["/admin"],
            };
        });
        var nextCalled = false;
        var middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            catalog
        );
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

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
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
    public async Task InvokeAsync_PageEndpoint_CallsNextAndSetsCheckedEndpoint()
    {
        var httpContext = CreateContext(guarded: false);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.SetEndpoint(
            new Endpoint(null, new EndpointMetadataCollection(new BffPageAttribute()), "page")
        );
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
    public async Task InvokeAsync_HubFromForeignOrigin_Returns403CrossSite()
    {
        var httpContext = CreateContext(guarded: false);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Headers.Origin = "https://evil.example.com";
        httpContext.SetEndpoint(
            new Endpoint(null, new EndpointMetadataCollection(new BffHubAttribute()), "hub")
        );
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
                    BffRequestGuard.CrossSiteRequest,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await middleware.InvokeAsync(httpContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(
            BffProblemTypes.CrossSiteRequest,
            ReadProblem(httpContext).GetProperty("type").GetString()
        );
    }

    [Fact]
    public async Task InvokeAsync_HubFromPublicOrigin_CallsNextAndSetsCheckedEndpoint()
    {
        var httpContext = CreateContext(guarded: false);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Headers.Origin = "https://app.example.com";
        httpContext.SetEndpoint(
            new Endpoint(null, new EndpointMetadataCollection(new BffHubAttribute()), "hub")
        );
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
    public async Task InvokeAsync_PagePostWithoutValidToken_Returns403TokenInvalid()
    {
        var httpContext = CreatePageContext(HttpMethods.Post);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        MockResolver
            .Setup(x => x.ResolveAsync(httpContext, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BffSessionState?)null)
            .Verifiable();
        MockAntiforgery
            .Setup(x => x.IsRequestValidAsync(httpContext))
            .ReturnsAsync(false)
            .Verifiable();
        MockRecorder
            .Setup(x =>
                x.AntiforgeryRejectedAsync(
                    httpContext,
                    BffRequestGuard.AntiforgeryTokenInvalid,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await middleware.InvokeAsync(httpContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(
            BffProblemTypes.AntiforgeryTokenInvalid,
            ReadProblem(httpContext).GetProperty("type").GetString()
        );
    }

    [Fact]
    public async Task InvokeAsync_PagePostWithInvalidTokenException_Returns403TokenInvalid()
    {
        var httpContext = CreatePageContext(HttpMethods.Post);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        MockResolver
            .Setup(x => x.ResolveAsync(httpContext, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BffSessionState?)null)
            .Verifiable();
        MockAntiforgery
            .Setup(x => x.IsRequestValidAsync(httpContext))
            .ThrowsAsync(new AntiforgeryValidationException("invalid"))
            .Verifiable();
        MockRecorder
            .Setup(x =>
                x.AntiforgeryRejectedAsync(
                    httpContext,
                    BffRequestGuard.AntiforgeryTokenInvalid,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(
            BffProblemTypes.AntiforgeryTokenInvalid,
            ReadProblem(httpContext).GetProperty("type").GetString()
        );
    }

    [Fact]
    public async Task InvokeAsync_PagePostWithValidToken_CallsNext()
    {
        var httpContext = CreatePageContext(HttpMethods.Post);
        var original = httpContext.User;
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        ClaimsPrincipal? seen = null;
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        MockResolver
            .Setup(x => x.ResolveAsync(httpContext, It.IsAny<CancellationToken>()))
            .Returns(
                (HttpContext context, CancellationToken _) =>
                {
                    var feature = context.Features.Get<BffRequestFeature>();
                    Assert.NotNull(feature);
                    feature.Principal = principal;
                    return ValueTask.FromResult<BffSessionState?>(null);
                }
            )
            .Verifiable();
        MockAntiforgery
            .Setup(x => x.IsRequestValidAsync(httpContext))
            .Returns(
                (HttpContext context) =>
                {
                    seen = context.User;
                    return Task.FromResult(true);
                }
            )
            .Verifiable();

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
        Assert.Same(principal, seen);
        Assert.Same(original, httpContext.User);
    }

    [Fact]
    public async Task InvokeAsync_PagePostCrossSite_Returns403CrossSite()
    {
        var httpContext = CreatePageContext(HttpMethods.Post);
        httpContext.Request.Headers["Sec-Fetch-Site"] = "cross-site";
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
                    BffRequestGuard.CrossSiteRequest,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await middleware.InvokeAsync(httpContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(
            BffProblemTypes.CrossSiteRequest,
            ReadProblem(httpContext).GetProperty("type").GetString()
        );
    }

    [Fact]
    public async Task InvokeAsync_PageGet_SkipsTokenValidation()
    {
        var httpContext = CreatePageContext(HttpMethods.Get);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_PagePostStoreUnavailable_Returns503()
    {
        var httpContext = CreatePageContext(HttpMethods.Post);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        MockResolver
            .Setup(x => x.ResolveAsync(httpContext, It.IsAny<CancellationToken>()))
            .Returns(() =>
                throw new BffStoreUnavailableException("down", new InvalidOperationException())
            )
            .Verifiable();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpContext.Response.StatusCode);
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
