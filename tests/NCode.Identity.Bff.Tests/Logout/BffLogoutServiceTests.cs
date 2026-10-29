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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
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
                Frontend = BffDefaults.FrontendName,
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
        var options = Microsoft.Extensions.Options.Options.Create(
            new BffOptions { Authority = "https://op.example.com", ClientId = "bff" }
        );
        var manager = CreateStrictMock<IBffSessionManager>();
        var revocation = CreateStrictMock<ITokenRevocationClient>();
        var service = new BffLogoutService(
            manager.Object,
            revocation.Object,
            new BffClientRegistrationProvider(options),
            new BffCookies(options, new MutableTimeProvider(Now)),
            CreateLooseMock<ILogger<BffLogoutService>>().Object
        );
        return (service, manager, revocation);
    }

    #region EndSessionAsync Tests

    [Fact]
    public async Task EndSessionAsync_WithRefreshToken_RevokesThenEnds()
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
                    It.IsAny<CancellationToken>()
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

        await service.EndSessionAsync(
            httpContext,
            state,
            BffSessionEndReasons.LoggedOut,
            CancellationToken.None
        );

        Assert.Equal(["revoke", "end"], order);
        Assert.True(feature.IsResolved);
        Assert.Null(feature.Session);
        Assert.Null(feature.Principal);
        Assert.Contains(
            BffDefaults.SessionCookieName,
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

        await service.EndSessionAsync(
            httpContext,
            state,
            BffSessionEndReasons.LoggedOut,
            CancellationToken.None
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

        await service.EndSessionAsync(
            httpContext,
            state,
            BffSessionEndReasons.LoggedOut,
            CancellationToken.None
        );
    }

    #endregion
}
