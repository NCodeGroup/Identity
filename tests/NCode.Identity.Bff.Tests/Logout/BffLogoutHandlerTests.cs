// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Logout;

public class BffLogoutHandlerTests : BaseTests
{
    private const string Guard = "guard-value";

    private static DateTimeOffset Now { get; } = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private readonly Mock<BffSessionResolver> _resolver;
    private readonly Mock<BffLogoutService> _logoutService;
    private readonly Mock<IProviderMetadataResolver> _metadataResolver;

    public BffLogoutHandlerTests()
    {
        var timeProvider = new FakeTimeProvider(Now);
        var cookies = new BffCookies(timeProvider);
        _resolver = CreateStrictMock<BffSessionResolver>(
            CreateStrictMock<IBffSessionManager>().Object,
            cookies,
            Array.Empty<IBffSessionValidator>(),
            CreateFrontends(),
            timeProvider
        );
        _logoutService = CreateStrictMock<BffLogoutService>(
            CreateStrictMock<IBffSessionManager>().Object,
            CreateStrictMock<ITokenRevocationClient>().Object,
            CreateFrontends(),
            cookies,
            CreateLooseMock<ILogger<BffLogoutService>>().Object
        );
        _metadataResolver = CreateStrictMock<IProviderMetadataResolver>();
    }

    private static BffFrontendCatalog CreateFrontends() =>
        BffTestFrontends.CreateCatalog(options =>
        {
            options.Authority = "https://op.example";
            options.PublicOrigin = "https://app.example";
            options.ClientId = "bff";
            options.ReturnUrls = ["/"];
        });

    private BffLogoutHandler CreateHandler() =>
        new(CreateFrontends(), _resolver.Object, _logoutService.Object, _metadataResolver.Object);

    private static BffSessionState CreateState() =>
        new(
            new string('a', 43),
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffTestFrontends.Name,
                Authority = "https://op.example",
                ClientId = "bff",
                Issuer = "https://op.example",
                Subject = "sub-1",
                SessionId = "sid-1",
                CreatedAt = Now,
                LastAccessedAt = Now,
                AbsoluteExpiresAt = Now.AddHours(8),
                IdleExpiresAt = Now.AddHours(1),
            },
            new BffSessionPayload { IdToken = "id-token", LogoutGuard = Guard }
        );

    private static DefaultHttpContext CreateHttpContext(string form)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Body = new MemoryStream(SecureEncoding.UTF8.GetBytes(form));
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private void SetupResolve(BffSessionState? state) =>
        _resolver
            .Setup(x => x.ResolveAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_DisallowedReturnUrl_WritesBadRequest()
    {
        var httpContext = CreateHttpContext("returnUrl=https%3A%2F%2Fevil.example%2F");
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_NoSession_RedirectsToReturnUrl()
    {
        SetupResolve(null);
        var httpContext = CreateHttpContext("returnUrl=%2Fhome");
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/home", httpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task HandleAsync_NotAFormPost_WritesBadRequest()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Response.Body = new MemoryStream();
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_FormOverTheKeyLengthLimit_WritesBadRequest()
    {
        var formKey = new string('k', 2049);
        var httpContext = CreateHttpContext($"{formKey}=value");
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sid=wrong-value")]
    [InlineData("sid=guard-value&sid=guard-value")]
    public async Task HandleAsync_BadSid_WritesBadRequestAndKeepsSession(string form)
    {
        SetupResolve(CreateState());
        var httpContext = CreateHttpContext(form);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SidInTheQueryString_WritesBadRequestAndKeepsSession()
    {
        SetupResolve(CreateState());
        var httpContext = CreateHttpContext("");
        httpContext.Request.QueryString = new QueryString("?sid=guard-value");
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        _logoutService.Verify(
            x =>
                x.EndSessionAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<BffSessionState>(),
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }

    private void SetupEnd(DefaultHttpContext httpContext, BffSessionState state) =>
        _logoutService
            .Setup(x => x.EndSessionAsync(httpContext, state, BffSessionEndReasons.LoggedOut))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    [Fact]
    public async Task HandleAsync_MatchingSid_RedirectsToEndSessionEndpoint()
    {
        var state = CreateState();
        SetupResolve(state);
        var httpContext = CreateHttpContext("sid=guard-value&returnUrl=%2Fhome");
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("attacker.example");
        SetupEnd(httpContext, state);
        _metadataResolver
            .Setup(x => x.GetMetadataAsync("https://op.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OpenIdProviderMetadata { EndSessionEndpoint = "https://op.example/logout" }
            )
            .Verifiable();
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        var location = new Uri(httpContext.Response.Headers.Location.ToString());
        Assert.Equal("https://op.example/logout", location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("id-token", query[OpenIdProtocol.Parameters.IdTokenHint]);
        Assert.Equal("bff", query[OpenIdProtocol.Parameters.ClientId]);
        Assert.Equal(
            "https://app.example/home",
            query[OpenIdProtocol.Parameters.PostLogoutRedirectUri]
        );
        Assert.False(string.IsNullOrEmpty(query[OpenIdProtocol.Parameters.State]));
    }

    [Fact]
    public async Task HandleAsync_NoEndSessionEndpoint_RedirectsToReturnUrl()
    {
        var state = CreateState();
        SetupResolve(state);
        var httpContext = CreateHttpContext("sid=guard-value");
        SetupEnd(httpContext, state);
        _metadataResolver
            .Setup(x => x.GetMetadataAsync("https://op.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenIdProviderMetadata())
            .Verifiable();
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/", httpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task HandleAsync_ProviderException_RedirectsToReturnUrl()
    {
        var state = CreateState();
        SetupResolve(state);
        var httpContext = CreateHttpContext("sid=guard-value");
        SetupEnd(httpContext, state);
        _metadataResolver
            .Setup(x => x.GetMetadataAsync("https://op.example", It.IsAny<CancellationToken>()))
            .Throws(new OpenIdProviderException("unavailable"))
            .Verifiable();
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/", httpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task HandleAsync_TransportFailure_RedirectsToReturnUrl()
    {
        var state = CreateState();
        SetupResolve(state);
        var httpContext = CreateHttpContext("sid=guard-value");
        SetupEnd(httpContext, state);
        _metadataResolver
            .Setup(x => x.GetMetadataAsync("https://op.example", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"))
            .Verifiable();
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/", httpContext.Response.Headers.Location.ToString());
    }

    #endregion
}
