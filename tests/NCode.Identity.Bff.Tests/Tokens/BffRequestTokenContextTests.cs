// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffRequestTokenContextTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);

    private BffRequestTokenContext CreateContext(
        HttpContext httpContext,
        BffFrontendCatalog catalog,
        BffSessionResolver resolver
    ) => new(httpContext, catalog, resolver, new BffCookies(_timeProvider));

    private Mock<BffSessionResolver> CreateResolver(BffFrontendCatalog catalog) =>
        CreateStrictMock<BffSessionResolver>(
            CreateStrictMock<IBffSessionManager>().Object,
            new BffCookies(_timeProvider),
            Array.Empty<IBffSessionValidator>(),
            catalog,
            _timeProvider
        );

    private static BffSessionState CreateState() =>
        new(
            new string('a', 43),
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffTestFrontends.Name,
                Issuer = "https://op.example",
                Subject = "sub-1",
                CreatedAt = Now.AddHours(-1),
                LastAccessedAt = Now.AddMinutes(-1),
                AbsoluteExpiresAt = Now.AddHours(7),
                IdleExpiresAt = Now.AddMinutes(30),
            },
            new BffSessionPayload { IdToken = "id-token", LogoutGuard = "guard" }
        );

    #region Frontend Tests

    [Fact]
    public void Frontend_SelectedFrontend_IsItsName()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var resolver = CreateResolver(catalog);
        var httpContext = new DefaultHttpContext();

        var context = CreateContext(httpContext, catalog, resolver.Object);

        Assert.Equal(BffTestFrontends.Name, context.Frontend);
        Assert.Same(BffTestFrontends.Get(catalog), context.SelectedFrontend);
        Assert.Same(httpContext, context.HttpContext);
    }

    [Fact]
    public void Frontend_NoFrontend_IsNull()
    {
        var catalog = BffTestFrontends.CreateCatalog(x =>
            x.Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Path,
                Values = ["/admin"],
            }
        );
        var resolver = CreateResolver(catalog);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api";

        var context = CreateContext(httpContext, catalog, resolver.Object);

        Assert.Null(context.Frontend);
        Assert.Null(context.SelectedFrontend);
    }

    #endregion

    #region ResolveAsync Tests

    [Fact]
    public async Task ResolveAsync_DelegatesToResolver()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var resolver = CreateResolver(catalog);
        var httpContext = new DefaultHttpContext();
        var state = CreateState();
        resolver
            .Setup(x => x.ResolveAsync(httpContext, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<BffSessionState?>(state))
            .Verifiable();
        var context = CreateContext(httpContext, catalog, resolver.Object);

        var result = await context.ResolveAsync(CancellationToken.None);

        Assert.Same(state, result);
    }

    #endregion

    #region OnSessionEnded Tests

    [Fact]
    public void OnSessionEnded_DeletesSessionCookie()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var resolver = CreateResolver(catalog);
        var httpContext = new DefaultHttpContext();
        var context = CreateContext(httpContext, catalog, resolver.Object);

        context.OnSessionEnded();

        var setCookie = Assert.Single(httpContext.Response.Headers.SetCookie.ToArray());
        Assert.StartsWith($"{BffTestFrontends.SessionCookieName}=;", setCookie);
    }

    [Fact]
    public void OnSessionEnded_NoFrontend_DeletesNothing()
    {
        var catalog = BffTestFrontends.CreateCatalog(x =>
            x.Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Path,
                Values = ["/admin"],
            }
        );
        var resolver = CreateResolver(catalog);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api";
        var context = CreateContext(httpContext, catalog, resolver.Object);

        context.OnSessionEnded();

        Assert.Empty(httpContext.Response.Headers.SetCookie.ToArray());
    }

    #endregion
}
