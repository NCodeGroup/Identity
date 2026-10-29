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
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class DefaultBffUserTokenServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static readonly string Handle = new('a', 43);

    private Mock<IBffSessionManager> MockManager { get; }
    private DefaultBffUserTokenService Service { get; }

    public DefaultBffUserTokenServiceTests()
    {
        MockManager = CreateStrictMock<IBffSessionManager>();
        var options = Microsoft.Extensions.Options.Options.Create(new BffOptions());
        var timeProvider = new MutableTimeProvider(Now);
        var cookies = new BffCookies(options, timeProvider);
        var resolver = new BffSessionResolver(
            MockManager.Object,
            cookies,
            [],
            options,
            timeProvider
        );
        var mockStore = CreateStrictMock<IBffSessionStore>();
        var mockTokenClient = CreateStrictMock<ITokenClient>();
        var mockRecorder = CreateStrictMock<IBffAuditRecorder>();
        Service = new DefaultBffUserTokenService(
            resolver,
            MockManager.Object,
            mockStore.Object,
            mockTokenClient.Object,
            new BffClientRegistrationProvider(options),
            new BffRefreshLocks(),
            cookies,
            mockRecorder.Object,
            options,
            timeProvider,
            CreateLooseMock<ILogger<DefaultBffUserTokenService>>().Object
        );
    }

    private static DefaultHttpContext CreateHttpContext(BffSessionState? session)
    {
        var httpContext = new DefaultHttpContext();
        var feature = new BffRequestFeature { IsResolved = true, Session = session };
        httpContext.Features.Set(feature);
        return httpContext;
    }

    private static BffSessionState CreateState(
        string? refreshToken,
        params BffStoredAccessToken[] accessTokens
    ) =>
        new(
            Handle,
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffDefaults.FrontendName,
                Issuer = "https://op.example",
                Subject = "sub-1",
                CreatedAt = Now.AddHours(-1),
                LastAccessedAt = Now.AddMinutes(-1),
                AbsoluteExpiresAt = Now.AddHours(7),
                IdleExpiresAt = Now.AddMinutes(59),
            },
            new BffSessionPayload
            {
                IdToken = "id-token",
                LogoutGuard = "guard",
                RefreshToken = refreshToken,
                AccessTokens = [.. accessTokens],
                Claims = [new BffStoredClaim { Type = "sub", Value = "sub-1" }],
            }
        );

    #region GetAccessTokenAsync Tests

    [Fact]
    public async Task GetAccessTokenAsync_NoSession_ReturnsSessionRequired()
    {
        var httpContext = CreateHttpContext(null);

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.SessionRequired, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_FreshCachedToken_ReturnsToken()
    {
        var cached = new BffStoredAccessToken
        {
            Requirement = BffTokenRequirementNames.User,
            Value = "access-token",
            TokenType = "DPoP",
            ExpiresAt = Now.AddMinutes(10),
            DPoPJwkThumbprint = "jkt-1",
        };
        var httpContext = CreateHttpContext(CreateState("refresh-token", cached));

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal("access-token", token.Value);
        Assert.Equal("DPoP", token.TokenType);
        Assert.Equal(cached.ExpiresAt, token.ExpiresAt);
        Assert.Equal("jkt-1", token.DPoPJwkThumbprint);
    }

    [Fact]
    public async Task GetAccessTokenAsync_StaleTokenWithoutRefreshToken_EndsSession()
    {
        var state = CreateState(null);
        var httpContext = CreateHttpContext(state);
        MockManager
            .Setup(x =>
                x.EndAsync(
                    httpContext,
                    state.StoreKey,
                    state.Metadata,
                    BffSessionEndReasons.RefreshFailed,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.RefreshFailed, result.Reason);
        Assert.Contains(
            httpContext.Response.Headers.SetCookie,
            x => x is not null && x.Contains(BffDefaults.SessionCookieName)
        );
    }

    [Fact]
    public async Task GetAccessTokenAsync_StaleTokenWithRefreshToken_RefreshIsNotImplemented()
    {
        var httpContext = CreateHttpContext(CreateState("refresh-token"));

        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await Service.GetAccessTokenAsync(httpContext, null, null, CancellationToken.None)
        );
    }

    #endregion
}
