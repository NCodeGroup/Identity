// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class DefaultBffConnectionSessionTests : BaseTests
{
    private const string Handle = "handle-1";
    private const string LogoutUrl = "/bff/logout";
    private const string AdminName = "admin";

    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);

    private Mock<BffSessionResolver> CreateResolver(BffFrontendCatalog catalog) =>
        CreateStrictMock<BffSessionResolver>(
            CreateStrictMock<IBffSessionManager>().Object,
            new BffCookies(_timeProvider),
            Array.Empty<IBffSessionValidator>(),
            catalog,
            _timeProvider
        );

    private static DefaultBffConnectionSession CreateSession(
        BffFrontendCatalog catalog,
        BffFrontend frontend,
        Mock<BffSessionResolver> resolver
    ) => CreateSession(catalog, frontend.Name, resolver);

    private static DefaultBffConnectionSession CreateSession(
        BffFrontendCatalog catalog,
        string frontend,
        Mock<BffSessionResolver> resolver
    ) => new(frontend, Handle, LogoutUrl, resolver.Object, catalog);

    private static BffOptions CreateTwoFrontends() =>
        BffTestFrontends.CreateBffOptions(x =>
            x.Frontends[AdminName] = BffTestFrontends.CreateOptions(y =>
                y.Selection = new BffFrontendSelectionOptions
                {
                    Rule = BffFrontendSelectorNames.Host,
                    Values = ["admin.example.com"],
                }
            )
        );

    private static BffSessionState CreateState() =>
        new(
            Handle,
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffTestFrontends.Name,
                Authority = BffTestFrontends.Authority,
                ClientId = BffTestFrontends.ClientId,
                Issuer = "https://op.example",
                Subject = "sub-1",
                CreatedAt = Now.AddHours(-1),
                LastAccessedAt = Now.AddMinutes(-1),
                AbsoluteExpiresAt = Now.AddHours(7),
                IdleExpiresAt = Now.AddMinutes(30),
            },
            new BffSessionPayload
            {
                IdToken = "id-token",
                LogoutGuard = "guard",
                Claims = [new BffStoredClaim { Type = "sub", Value = "sub-1" }],
            }
        );

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_SessionResolves_ReturnsPrincipalWithLogoutClaims()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        resolver
            .Setup(x =>
                x.ValidateAsync(frontend, Handle, null, null, It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<BffSessionState?>(CreateState()))
            .Verifiable();
        var session = CreateSession(catalog, frontend, resolver);

        var principal = await session.ValidateAsync(null, CancellationToken.None);

        Assert.NotNull(principal);
        Assert.Equal("sub-1", principal.FindFirst("sub")?.Value);
        Assert.Equal(LogoutUrl, principal.FindFirst(BffClaimTypes.LogoutUrl)?.Value);
        Assert.Equal("guard", principal.FindFirst(BffClaimTypes.LogoutSid)?.Value);
    }

    [Fact]
    public async Task ValidateAsync_SessionEnded_ReturnsNull()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        resolver
            .Setup(x =>
                x.ValidateAsync(frontend, Handle, null, null, It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<BffSessionState?>((BffSessionState?)null))
            .Verifiable();
        var session = CreateSession(catalog, frontend, resolver);

        var principal = await session.ValidateAsync(null, CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task ValidateAsync_SessionEnded_CancelsSessionEnded()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        resolver
            .Setup(x =>
                x.ValidateAsync(frontend, Handle, null, null, It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<BffSessionState?>((BffSessionState?)null))
            .Verifiable();
        var session = CreateSession(catalog, frontend, resolver);

        await session.ValidateAsync(null, CancellationToken.None);

        Assert.True(session.SessionEnded.IsCancellationRequested);
    }

    [Fact]
    public async Task ValidateAsync_AfterSessionEnded_ReturnsNullWithoutResolving()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var calls = 0;
        resolver
            .Setup(x =>
                x.ValidateAsync(frontend, Handle, null, null, It.IsAny<CancellationToken>())
            )
            .Returns(() =>
            {
                calls++;
                return new ValueTask<BffSessionState?>((BffSessionState?)null);
            })
            .Verifiable();
        var session = CreateSession(catalog, frontend, resolver);

        var first = await session.ValidateAsync(null, CancellationToken.None);
        var second = await session.ValidateAsync(null, CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ValidateAsync_PassesActivityWithoutHttpContext()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var activityAt = Now.AddSeconds(30);
        resolver
            .Setup(x =>
                x.ValidateAsync(frontend, Handle, null, activityAt, It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<BffSessionState?>(CreateState()))
            .Verifiable();
        var session = CreateSession(catalog, frontend, resolver);

        var principal = await session.ValidateAsync(activityAt, CancellationToken.None);

        Assert.NotNull(principal);
    }

    [Fact]
    public async Task ValidateAsync_FrontendRemovedByAChange_ReturnsNull()
    {
        var source = new BffTestOptionsSource(CreateTwoFrontends());
        using var catalog = BffTestFrontends.CreateCatalog(source);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, AdminName, resolver);

        source.Change(BffTestFrontends.CreateBffOptions());

        var principal = await session.ValidateAsync(null, CancellationToken.None);

        Assert.Null(principal);
        Assert.True(session.SessionEnded.IsCancellationRequested);
    }

    #endregion

    #region ResolveAsync Tests

    [Fact]
    public async Task ResolveAsync_SessionEnded_CancelsSessionEnded()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        resolver
            .Setup(x =>
                x.ValidateAsync(frontend, Handle, null, null, It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<BffSessionState?>((BffSessionState?)null))
            .Verifiable();
        var session = CreateSession(catalog, frontend, resolver);

        var result = await session.ResolveAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.True(session.SessionEnded.IsCancellationRequested);
    }

    [Fact]
    public async Task ResolveAsync_AfterSessionEnded_ReturnsNullWithoutResolving()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var calls = 0;
        resolver
            .Setup(x =>
                x.ValidateAsync(frontend, Handle, null, null, It.IsAny<CancellationToken>())
            )
            .Returns(() =>
            {
                calls++;
                return new ValueTask<BffSessionState?>((BffSessionState?)null);
            })
            .Verifiable();
        var session = CreateSession(catalog, frontend, resolver);

        var first = await session.ResolveAsync(CancellationToken.None);
        var second = await session.ResolveAsync(CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ResolveAsync_AfterOnSessionEnded_ReturnsNullWithoutResolving()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, frontend, resolver);
        session.OnSessionEnded();

        var result = await session.ResolveAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_ValidatesWithoutSliding()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var state = CreateState();
        resolver
            .Setup(x =>
                x.ValidateAsync(frontend, Handle, null, null, It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<BffSessionState?>(state))
            .Verifiable();
        var session = CreateSession(catalog, frontend, resolver);

        var result = await session.ResolveAsync(CancellationToken.None);

        Assert.Same(state, result);
    }

    #endregion

    #region RevalidationInterval Property Tests

    [Fact]
    public void RevalidationInterval_ReadsFrontendOption()
    {
        var catalog = BffTestFrontends.CreateCatalog(x =>
            x.Session.RevalidationInterval = TimeSpan.FromSeconds(20)
        );
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, frontend, resolver);

        Assert.Equal(TimeSpan.FromSeconds(20), session.RevalidationInterval);
    }

    [Fact]
    public void RevalidationInterval_FrontendRemovedByAChange_IsZero()
    {
        var source = new BffTestOptionsSource(CreateTwoFrontends());
        using var catalog = BffTestFrontends.CreateCatalog(source);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, AdminName, resolver);

        source.Change(BffTestFrontends.CreateBffOptions());

        Assert.Equal(TimeSpan.Zero, session.RevalidationInterval);
    }

    #endregion

    #region Frontend Property Tests

    [Fact]
    public void Frontend_ReturnsFrontendName()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, frontend, resolver);

        Assert.Equal(BffTestFrontends.Name, session.Frontend);
    }

    #endregion

    #region SelectedFrontend Property Tests

    [Fact]
    public void SelectedFrontend_ReturnsFrontend()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, frontend, resolver);

        Assert.Same(frontend, session.SelectedFrontend);
    }

    [Fact]
    public void SelectedFrontend_AfterAChange_IsTheCurrentInstance()
    {
        var source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        using var catalog = BffTestFrontends.CreateCatalog(source);
        var original = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, original, resolver);

        source.Change(BffTestFrontends.CreateBffOptions());

        var current = BffTestFrontends.Get(catalog);
        Assert.NotSame(original, current);
        Assert.Same(current, session.SelectedFrontend);
    }

    [Fact]
    public void SelectedFrontend_FrontendRemovedByAChange_IsNull()
    {
        var source = new BffTestOptionsSource(CreateTwoFrontends());
        using var catalog = BffTestFrontends.CreateCatalog(source);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, AdminName, resolver);

        source.Change(BffTestFrontends.CreateBffOptions());

        Assert.Null(session.SelectedFrontend);
    }

    #endregion

    #region HttpContext Property Tests

    [Fact]
    public void HttpContext_IsNull()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, frontend, resolver);

        Assert.Null(session.HttpContext);
    }

    #endregion

    #region OnSessionEnded Tests

    [Fact]
    public void OnSessionEnded_CancelsSessionEnded()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, frontend, resolver);

        session.OnSessionEnded();

        Assert.True(session.SessionEnded.IsCancellationRequested);
    }

    #endregion

    #region SessionEnded Property Tests

    [Fact]
    public void SessionEnded_WhileTheSessionLives_IsNotCancelled()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = CreateSession(catalog, frontend, resolver);

        Assert.False(session.SessionEnded.IsCancellationRequested);
    }

    #endregion
}
