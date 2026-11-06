// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class DefaultBffConnectionSessionTests : BaseTests
{
    private const string Handle = "handle-1";
    private const string LogoutUrl = "/bff/logout";

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

    private static BffSessionState CreateState() =>
        new(
            Handle,
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
        var session = new DefaultBffConnectionSession(frontend, Handle, LogoutUrl, resolver.Object);

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
        var session = new DefaultBffConnectionSession(frontend, Handle, LogoutUrl, resolver.Object);

        var principal = await session.ValidateAsync(null, CancellationToken.None);

        Assert.Null(principal);
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
        var session = new DefaultBffConnectionSession(frontend, Handle, LogoutUrl, resolver.Object);

        var principal = await session.ValidateAsync(activityAt, CancellationToken.None);

        Assert.NotNull(principal);
    }

    #endregion

    #region ResolveAsync Tests

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
        var session = new DefaultBffConnectionSession(frontend, Handle, LogoutUrl, resolver.Object);

        var result = await session.ResolveAsync(CancellationToken.None);

        Assert.Same(state, result);
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void RevalidationInterval_ReadsFrontendOption()
    {
        var catalog = BffTestFrontends.CreateCatalog(x =>
            x.Session.RevalidationInterval = TimeSpan.FromSeconds(20)
        );
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = new DefaultBffConnectionSession(frontend, Handle, LogoutUrl, resolver.Object);

        Assert.Equal(TimeSpan.FromSeconds(20), session.RevalidationInterval);
    }

    [Fact]
    public void Properties_DescribeFrontendWithoutRequest()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = new DefaultBffConnectionSession(frontend, Handle, LogoutUrl, resolver.Object);

        Assert.Equal(BffTestFrontends.Name, session.Frontend);
        Assert.Same(frontend, session.SelectedFrontend);
        Assert.Null(session.HttpContext);
    }

    #endregion

    #region OnSessionEnded Tests

    [Fact]
    public void OnSessionEnded_DoesNothing()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var session = new DefaultBffConnectionSession(frontend, Handle, LogoutUrl, resolver.Object);

        session.OnSessionEnded();
    }

    #endregion

    #region BffConnectionSessions Tests

    [Fact]
    public void Create_ReturnsConnectionSessionOfFrontend()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var sessions = new BffConnectionSessions(resolver.Object);

        var session = sessions.Create(frontend, Handle, LogoutUrl);

        Assert.Equal(BffTestFrontends.Name, session.Frontend);
        Assert.Null(session.HttpContext);
    }

    #endregion
}
