// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Authentication;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Jose;
using Xunit;

namespace NCode.Identity.Bff.Tests.Authentication;

public class BffAuthenticationHandlerTests : BaseTests
{
    private const string PageReloadCookie = "__Host-Http-bff-reload.default=1";

    private static DateTimeOffset Now { get; } = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static BffSessionState CreateState() =>
        new(
            new string('a', 43),
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffTestFrontends.Name,
                Authority = BffTestFrontends.Authority,
                ClientId = BffTestFrontends.ClientId,
                Issuer = "https://op.example",
                Subject = "sub-1",
                CreatedAt = Now,
                LastAccessedAt = Now,
                AbsoluteExpiresAt = Now.AddHours(8),
                IdleExpiresAt = Now.AddHours(1),
            },
            new BffSessionPayload
            {
                IdToken = "id-token",
                LogoutGuard = "guard",
                Claims =
                [
                    new BffStoredClaim { Type = JoseClaimNames.Payload.Sub, Value = "sub-1" },
                ],
            }
        );

    private async ValueTask<(
        BffAuthenticationHandler Handler,
        DefaultHttpContext HttpContext
    )> CreateHandlerAsync(
        BffSessionState? session,
        object? endpointMetadata,
        Action<BffFrontendOptions>? configure,
        BffFrontendCatalog? catalog,
        CancellationToken cancellationToken
    )
    {
        var timeProvider = new FakeTimeProvider(Now);
        var cookies = new BffCookies(timeProvider);
        var manager = CreateStrictMock<IBffSessionManager>();
        catalog ??= BffTestFrontends.CreateCatalog(configure);
        var resolver = new BffSessionResolver(manager.Object, cookies, [], catalog, timeProvider);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/";
        httpContext.Response.Body = new MemoryStream();
        var feature = new BffRequestFeature
        {
            IsResolved = true,
            Session = session,
            Principal = session is null ? null : BffSessionPrincipal.Create(session, "/bff/logout"),
        };
        if (endpointMetadata is not null)
        {
            var endpoint = new Endpoint(
                null,
                new EndpointMetadataCollection(endpointMetadata),
                "test"
            );
            httpContext.SetEndpoint(endpoint);
            feature.CheckedEndpoint = endpoint;
        }

        httpContext.Features.Set(feature);

        var schemeOptions = CreateStrictMock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        schemeOptions
            .Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AuthenticationSchemeOptions())
            .Verifiable();

        var handler = new BffAuthenticationHandler(
            schemeOptions.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            resolver,
            catalog,
            new BffConnectionSessions(resolver, catalog),
            cookies,
            new BffPageReload(cookies)
        );
        var scheme = new AuthenticationScheme(
            BffDefaults.AuthenticationScheme,
            null,
            typeof(BffAuthenticationHandler)
        );
        await handler.InitializeAsync(scheme, httpContext);
        return (handler, httpContext);
    }

    private static async ValueTask<string> ReadBodyAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, SecureEncoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    #region AuthenticateAsync Tests

    [Fact]
    public async Task AuthenticateAsync_WithSession_Succeeds()
    {
        var (handler, _) = await CreateHandlerAsync(
            CreateState(),
            new BffApiAttribute(),
            null,
            null,
            CancellationToken.None
        );

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(BffDefaults.AuthenticationScheme, result.Ticket?.AuthenticationScheme);
        Assert.Equal("sub-1", result.Principal?.FindFirstValue(JoseClaimNames.Payload.Sub));
    }

    [Fact]
    public async Task AuthenticateAsync_PageEndpoint_Succeeds()
    {
        var (handler, _) = await CreateHandlerAsync(
            CreateState(),
            new BffPageAttribute(),
            null,
            null,
            CancellationToken.None
        );

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(BffDefaults.AuthenticationScheme, result.Ticket?.AuthenticationScheme);
        Assert.Equal("sub-1", result.Principal?.FindFirstValue(JoseClaimNames.Payload.Sub));
    }

    [Fact]
    public async Task AuthenticateAsync_HubEndpoint_PrincipalCarriesConnectionSession()
    {
        var (handler, _) = await CreateHandlerAsync(
            CreateState(),
            new BffHubAttribute(),
            null,
            null,
            CancellationToken.None
        );

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("sub-1", result.Principal?.FindFirstValue(JoseClaimNames.Payload.Sub));
        Assert.Equal("/bff/logout", result.Principal?.FindFirstValue(BffClaimTypes.LogoutUrl));
        var connection = result.Principal?.GetBffConnectionSession();
        Assert.NotNull(connection);
        Assert.Equal(BffTestFrontends.Name, connection.Frontend);
    }

    [Fact]
    public async Task AuthenticateAsync_PageEndpoint_PrincipalCarriesNoConnectionSession()
    {
        var (handler, _) = await CreateHandlerAsync(
            CreateState(),
            new BffPageAttribute(),
            null,
            null,
            CancellationToken.None
        );

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal?.GetBffConnectionSession());
    }

    [Fact]
    public async Task AuthenticateAsync_UnguardedEndpoint_ReturnsNoResult()
    {
        var (handler, _) = await CreateHandlerAsync(
            CreateState(),
            null,
            null,
            null,
            CancellationToken.None
        );

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
    }

    [Fact]
    public async Task AuthenticateAsync_NoSession_ReturnsNoResult()
    {
        var (handler, _) = await CreateHandlerAsync(
            null,
            new BffApiAttribute(),
            null,
            null,
            CancellationToken.None
        );

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
    }

    #endregion

    #region ChallengeAsync Tests

    [Fact]
    public async Task ChallengeAsync_ApiEndpoint_WritesUnauthorizedProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffApiAttribute(),
            null,
            null,
            CancellationToken.None
        );

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
        var body = await ReadBodyAsync(httpContext, CancellationToken.None);
        Assert.Contains(BffProblemTypes.SessionRequired, body);
    }

    [Fact]
    public async Task ChallengeAsync_HubEndpoint_WritesUnauthorizedProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffHubAttribute(),
            null,
            null,
            CancellationToken.None
        );

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
        var body = await ReadBodyAsync(httpContext, CancellationToken.None);
        Assert.Contains(BffProblemTypes.SessionRequired, body);
    }

    [Fact]
    public async Task ChallengeAsync_PageEndpoint_AllowedPath_RedirectsToLoginWithReturnUrl()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            f => f.ReturnUrls = ["/app"],
            null,
            CancellationToken.None
        );
        httpContext.Request.Path = "/app/page";
        httpContext.Request.QueryString = new QueryString("?a=1");
        httpContext.Request.Headers.Cookie = PageReloadCookie;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            "/bff/login?returnUrl=%2Fapp%2Fpage%3Fa%3D1",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task ChallengeAsync_PageEndpoint_FrontendWithPathPrefix_RedirectsToItsLogin()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            f =>
            {
                f.PathPrefix = "/admin/bff";
                f.ReturnUrls = ["/admin"];
            },
            null,
            CancellationToken.None
        );
        httpContext.Request.Path = "/admin/page";
        httpContext.Request.Headers.Cookie = PageReloadCookie;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            "/admin/bff/login?returnUrl=%2Fadmin%2Fpage",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task ChallengeAsync_PageEndpoint_DisallowedPath_RedirectsToLoginWithRoot()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            f => f.ReturnUrls = ["/app"],
            null,
            CancellationToken.None
        );
        httpContext.Request.Path = "/other";
        httpContext.Request.Headers.Cookie = PageReloadCookie;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ChallengeAsync_PageWithoutAFrontend_Answers401()
    {
        var catalog = BffTestFrontends.CreateCatalog(f =>
            f.Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Path,
                Values = ["/admin"],
            }
        );
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            null,
            catalog,
            CancellationToken.None
        );
        httpContext.Request.Path = "/other";

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
        var body = await ReadBodyAsync(httpContext, CancellationToken.None);
        Assert.Contains(BffProblemTypes.SessionRequired, body);
    }

    [Fact]
    public async Task ChallengeAsync_PageNavigationWhenPageReloadApplies_WritesReloadPageAndSetsTheMarker()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            null,
            null,
            CancellationToken.None
        );
        httpContext.Request.Path = "/app/page";

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", httpContext.Response.ContentType);
        Assert.False(httpContext.Response.Headers.ContainsKey("Location"));
        Assert.StartsWith(PageReloadCookie, httpContext.Response.Headers.SetCookie.ToString());
        var body = await ReadBodyAsync(httpContext, CancellationToken.None);
        Assert.Contains("url=/app/page\"", body);
    }

    [Fact]
    public async Task ChallengeAsync_PageNavigationWithReloadMarker_RedirectsToLoginAndDeletesTheMarker()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            null,
            null,
            CancellationToken.None
        );
        httpContext.Request.Headers.Cookie = PageReloadCookie;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
        Assert.StartsWith(
            "__Host-Http-bff-reload.default=;",
            httpContext.Response.Headers.SetCookie.ToString()
        );
    }

    [Fact]
    public async Task ChallengeAsync_UnguardedEndpoint_Throws()
    {
        var (handler, _) = await CreateHandlerAsync(null, null, null, null, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ChallengeAsync(null)
        );

        Assert.Contains("does not guard", exception.Message);
    }

    #endregion

    #region SignOutAsync Tests

    [Fact]
    public async Task SignOutAsync_UnguardedEndpoint_Throws()
    {
        var (handler, _) = await CreateHandlerAsync(
            CreateState(),
            null,
            null,
            null,
            CancellationToken.None
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SignOutAsync(null)
        );

        Assert.Contains("AsBffApi() or [BffApi]", exception.Message);
    }

    #endregion

    #region ForbidAsync Tests

    [Fact]
    public async Task ForbidAsync_ApiEndpoint_WritesForbiddenProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffApiAttribute(),
            null,
            null,
            CancellationToken.None
        );

        await handler.ForbidAsync(null);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        var body = await ReadBodyAsync(httpContext, CancellationToken.None);
        Assert.Contains(BffProblemTypes.Forbidden, body);
    }

    [Fact]
    public async Task ForbidAsync_HubEndpoint_WritesForbiddenProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffHubAttribute(),
            null,
            null,
            CancellationToken.None
        );

        await handler.ForbidAsync(null);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        var body = await ReadBodyAsync(httpContext, CancellationToken.None);
        Assert.Contains(BffProblemTypes.Forbidden, body);
    }

    #endregion
}
