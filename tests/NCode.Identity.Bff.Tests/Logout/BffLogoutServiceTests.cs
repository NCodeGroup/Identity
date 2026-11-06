// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Logout;

public class BffLogoutServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static BffSessionState CreateState(string? refreshToken) =>
        new(
            new string('a', 43),
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffTestFrontends.Name,
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
                RefreshToken = refreshToken,
                LogoutGuard = "guard",
            }
        );

    private (
        BffLogoutService Service,
        Mock<IBffSessionManager> Manager,
        Mock<ITokenRevocationClient> Revocation
    ) CreateService()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new BffOptions());
        var manager = CreateStrictMock<IBffSessionManager>();
        var revocation = CreateStrictMock<ITokenRevocationClient>();
        var service = new BffLogoutService(
            manager.Object,
            revocation.Object,
            BffTestFrontends.CreateCatalog(),
            new BffCookies(options, new FakeTimeProvider(Now)),
            CreateLooseMock<ILogger<BffLogoutService>>().Object
        );
        return (service, manager, revocation);
    }

    #region EndSessionAsync Tests

    [Fact]
    public async Task EndSessionAsync_WithRefreshToken_EndsThenRevokes()
    {
        var (service, manager, revocation) = CreateService();
        var state = CreateState("refresh-token");
        var httpContext = new DefaultHttpContext();
        var feature = new BffRequestFeature { IsResolved = true, Session = state };
        httpContext.Features.Set(feature);
        var order = new List<string>();

        revocation
            .Setup(x =>
                x.RevokeTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.Is<TokenRevocationRequest>(r =>
                        r.Token == "refresh-token"
                        && r.TokenTypeHint == OpenIdProtocol.TokenTypeHints.RefreshToken
                    ),
                    CancellationToken.None
                )
            )
            .Callback(() => order.Add("revoke"))
            .Returns(new ValueTask<ProtocolError?>((ProtocolError?)null))
            .Verifiable();
        manager
            .Setup(x =>
                x.EndAsync(
                    httpContext,
                    state.StoreKey,
                    state.Metadata,
                    BffSessionEndReasons.LoggedOut,
                    CancellationToken.None
                )
            )
            .Callback(() => order.Add("end"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await service.EndSessionAsync(httpContext, state, BffSessionEndReasons.LoggedOut);

        Assert.Equal(["end", "revoke"], order);
        Assert.True(feature.IsResolved);
        Assert.Null(feature.Session);
        Assert.Null(feature.Principal);
        Assert.Contains(
            BffTestFrontends.SessionCookieName,
            httpContext.Response.Headers.SetCookie.ToString()
        );
    }

    [Fact]
    public async Task EndSessionAsync_RevocationThrows_StillEnds()
    {
        var (service, manager, revocation) = CreateService();
        var state = CreateState("refresh-token");
        var httpContext = new DefaultHttpContext();

        revocation
            .Setup(x =>
                x.RevokeTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRevocationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new HttpRequestException("down"))
            .Verifiable();
        manager
            .Setup(x =>
                x.EndAsync(
                    httpContext,
                    state.StoreKey,
                    state.Metadata,
                    BffSessionEndReasons.LoggedOut,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await service.EndSessionAsync(httpContext, state, BffSessionEndReasons.LoggedOut);
    }

    [Fact]
    public async Task EndSessionAsync_RevocationCanceled_StillEndsAndDeletesTheCookie()
    {
        var (service, manager, revocation) = CreateService();
        var state = CreateState("refresh-token");
        var httpContext = new DefaultHttpContext();

        revocation
            .Setup(x =>
                x.RevokeTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRevocationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new OperationCanceledException())
            .Verifiable();
        manager
            .Setup(x =>
                x.EndAsync(
                    httpContext,
                    state.StoreKey,
                    state.Metadata,
                    BffSessionEndReasons.LoggedOut,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await service.EndSessionAsync(httpContext, state, BffSessionEndReasons.LoggedOut);

        Assert.Contains(
            BffTestFrontends.SessionCookieName,
            httpContext.Response.Headers.SetCookie.ToString()
        );
    }

    [Fact]
    public async Task EndSessionAsync_NoRefreshToken_SkipsRevocation()
    {
        var (service, manager, _) = CreateService();
        var state = CreateState(null);
        var httpContext = new DefaultHttpContext();

        manager
            .Setup(x =>
                x.EndAsync(
                    httpContext,
                    state.StoreKey,
                    state.Metadata,
                    BffSessionEndReasons.LoggedOut,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await service.EndSessionAsync(httpContext, state, BffSessionEndReasons.LoggedOut);
    }

    #endregion
}
