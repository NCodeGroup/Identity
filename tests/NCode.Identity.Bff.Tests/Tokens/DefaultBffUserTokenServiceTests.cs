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
using NCode.Identity.Jose;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class DefaultBffUserTokenServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static readonly string Handle = new('a', 43);

    private static readonly string LeaseKey =
        "store-key:" + BffKeyDerivation.Fingerprint("refresh-token");

    private Mock<IBffSessionManager> MockManager { get; }
    private Mock<IBffSessionStore> MockStore { get; }
    private Mock<ITokenClient> MockTokenClient { get; }
    private Mock<IBffAuditRecorder> MockRecorder { get; }
    private MutableTimeProvider TimeProvider { get; }
    private DefaultBffUserTokenService Service { get; }

    public DefaultBffUserTokenServiceTests()
    {
        MockManager = CreateStrictMock<IBffSessionManager>();
        var options = Microsoft.Extensions.Options.Options.Create(
            new BffOptions { Authority = "https://op.example", ClientId = "bff" }
        );
        var timeProvider = new MutableTimeProvider(Now);
        TimeProvider = timeProvider;
        var cookies = new BffCookies(options, timeProvider);
        var resolver = new BffSessionResolver(
            MockManager.Object,
            cookies,
            [],
            options,
            timeProvider
        );
        MockStore = CreateStrictMock<IBffSessionStore>();
        MockTokenClient = CreateStrictMock<ITokenClient>();
        var mockRecorder = CreateStrictMock<IBffAuditRecorder>();
        MockRecorder = mockRecorder;
        Service = new DefaultBffUserTokenService(
            resolver,
            MockManager.Object,
            MockStore.Object,
            MockTokenClient.Object,
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
                DPoPJwkThumbprint = "jkt-1",
                AccessTokens = [.. accessTokens],
                Claims =
                [
                    new BffStoredClaim { Type = JoseClaimNames.Payload.Sub, Value = "sub-1" },
                ],
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
    public async Task GetAccessTokenAsync_WonLease_RefreshesAndWritesRotatedTokens()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupLeaseAcquired();
        const string resource = "https://api.example";
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.Is<TokenRequest>(r =>
                        r is RefreshTokenRequest
                        && ((RefreshTokenRequest)r).RefreshToken == "refresh-token"
                        && r.Resources.SequenceEqual(new[] { resource })
                        && r.Scope == "read"
                        && r.DPoPJwkThumbprint == "jkt-1"
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new TokenResult(
                    new TokenResponse
                    {
                        AccessToken = "new-access",
                        TokenType = "DPoP",
                        RefreshToken = "rotated-refresh",
                        ExpiresIn = 300,
                    }
                )
                {
                    DPoPJwkThumbprint = "jkt-2",
                }
            )
            .Verifiable();
        BffSessionState? written = null;
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (
                    BffSessionState current,
                    Func<BffSessionState, BffSessionState?> update,
                    CancellationToken _
                ) =>
                {
                    written = update(current);
                    return ValueTask.FromResult(written);
                }
            )
            .Verifiable();
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync(LeaseKey, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            resource,
            "read",
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal("new-access", token.Value);
        Assert.Equal("DPoP", token.TokenType);
        Assert.Equal(Now.AddSeconds(300), token.ExpiresAt);
        Assert.Equal("jkt-2", token.DPoPJwkThumbprint);
        Assert.NotNull(written);
        Assert.Equal("rotated-refresh", written.Payload.RefreshToken);
        var entry = Assert.Single(written.Payload.AccessTokens);
        Assert.Equal(BffTokenRequirementNames.User, entry.Requirement);
        Assert.Equal(resource, entry.Resource);
        Assert.Equal("read", entry.Scope);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WriteMergedOntoNewerVersion_KeepsNewerRefreshToken()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupLeaseAcquired();
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<RefreshTokenRequest>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new TokenResult(
                    new TokenResponse
                    {
                        AccessToken = "new-access",
                        TokenType = "Bearer",
                        RefreshToken = "rotated-refresh",
                        ExpiresIn = 300,
                    }
                )
            )
            .Verifiable();
        var newer = CreateState("newer-refresh") with { Version = 2 };
        BffSessionState? written = null;
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (
                    BffSessionState _,
                    Func<BffSessionState, BffSessionState?> update,
                    CancellationToken _
                ) =>
                {
                    written = update(newer);
                    return ValueTask.FromResult(written);
                }
            )
            .Verifiable();
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync(LeaseKey, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(written);
        Assert.Equal("newer-refresh", written.Payload.RefreshToken);
        Assert.Single(written.Payload.AccessTokens);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RefreshSlowerThanMargin_KeepsLease()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupLeaseAcquired();
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<RefreshTokenRequest>(),
                    CancellationToken.None
                )
            )
            .Returns(() =>
            {
                TimeProvider.UtcNow = Now.AddSeconds(36);
                var response = new TokenResponse
                {
                    AccessToken = "new-access",
                    TokenType = "Bearer",
                    ExpiresIn = 300,
                };
                return ValueTask.FromResult(new TokenResult(response));
            })
            .Verifiable();
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (
                    BffSessionState current,
                    Func<BffSessionState, BffSessionState?> update,
                    CancellationToken _
                ) => ValueTask.FromResult(update(current))
            )
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        MockStore.Verify(
            x => x.ReleaseRefreshLeaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task GetAccessTokenAsync_SessionGoneOnReload_ReturnsSessionRequired()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        MockManager
            .Setup(x => x.LoadAsync(Handle, CancellationToken.None))
            .ReturnsAsync((BffSessionState?)null)
            .Verifiable();

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
    public async Task GetAccessTokenAsync_LostLeaseThenFreshToken_ReturnsTokenWithoutRefreshing()
    {
        var service = CreateWaitingService();
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        var fresh = CreateState(
            "refresh-token",
            new BffStoredAccessToken
            {
                Requirement = BffTokenRequirementNames.User,
                Value = "winner-access",
                TokenType = "Bearer",
                // The waiting service runs on the system clock, because its backoff waits in real time.
                ExpiresAt = System.TimeProvider.System.GetUtcNow().AddHours(1),
            }
        );
        MockManager
            .SetupSequence(x => x.LoadAsync(Handle, CancellationToken.None))
            .ReturnsAsync(state)
            .ReturnsAsync(fresh);
        SetupLeaseLost();

        var result = await service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal("winner-access", token.Value);
        MockTokenClient.Verify(
            x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GetAccessTokenAsync_LostLeaseThenChangedRefreshToken_RefreshesWithNewToken()
    {
        var service = CreateWaitingService();
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        var changed = CreateState("changed-refresh");
        MockManager
            .SetupSequence(x => x.LoadAsync(Handle, CancellationToken.None))
            .ReturnsAsync(state)
            .ReturnsAsync(changed);
        SetupLeaseLost();
        var changedLeaseKey = "store-key:" + BffKeyDerivation.Fingerprint("changed-refresh");
        MockStore
            .Setup(x =>
                x.TryAcquireRefreshLeaseAsync(
                    changedLeaseKey,
                    It.IsAny<DateTimeOffset>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(true)
            .Verifiable();
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.Is<TokenRequest>(r =>
                        r is RefreshTokenRequest
                        && ((RefreshTokenRequest)r).RefreshToken == "changed-refresh"
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new TokenResult(
                    new TokenResponse
                    {
                        AccessToken = "new-access",
                        TokenType = "Bearer",
                        ExpiresIn = 300,
                    }
                )
            )
            .Verifiable();
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    changed,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (
                    BffSessionState current,
                    Func<BffSessionState, BffSessionState?> update,
                    CancellationToken _
                ) => ValueTask.FromResult(update(current))
            )
            .Verifiable();

        var result = await service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal("new-access", token.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_LeaseStaysHeld_ReturnsRefreshWaitTimeout()
    {
        var service = CreateWaitingService();
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        MockManager
            .Setup(x => x.LoadAsync(Handle, CancellationToken.None))
            .ReturnsAsync(state)
            .Verifiable();
        SetupLeaseLost();
        MockStore
            .Setup(x => x.IsRefreshLeaseHeldAsync(LeaseKey, CancellationToken.None))
            .ReturnsAsync(true)
            .Verifiable();

        var result = await service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.RefreshWaitTimeout, result.Reason);
        MockTokenClient.Verify(
            x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GetAccessTokenAsync_InvalidGrant_EndsSessionAndReturnsRefreshFailed()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupFailedRefresh(state, httpContext, OpenIdProtocol.ErrorCodes.InvalidGrant);
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
    public async Task GetAccessTokenAsync_OtherOAuthError_ReturnsProviderError()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupFailedRefresh(state, httpContext, OpenIdProtocol.ErrorCodes.InvalidScope);

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(502, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.ProviderError, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_HttpRequestException_ReturnsProviderUnavailable() =>
        await AssertProviderUnavailableAsync(new HttpRequestException("down"));

    [Fact]
    public async Task GetAccessTokenAsync_OpenIdProviderException_ReturnsProviderUnavailable() =>
        await AssertProviderUnavailableAsync(new OpenIdProviderException("bad metadata"));

    [Fact]
    public async Task GetAccessTokenAsync_TimeoutWithoutCallerCancellation_ReturnsProviderUnavailable() =>
        await AssertProviderUnavailableAsync(new TaskCanceledException("timed out"));

    [Fact]
    public async Task GetAccessTokenAsync_SuccessWithoutAccessToken_ReturnsProviderError()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupLeaseAcquired();
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new TokenResult(new TokenResponse { TokenType = "Bearer" }))
            .Verifiable();
        MockRecorder
            .Setup(x =>
                x.TokenRefreshFailedAsync(
                    httpContext,
                    "sub-1",
                    BffTokenFailureReasons.ProviderError,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync(LeaseKey, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(502, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.ProviderError, result.Reason);
    }

    private async Task AssertProviderUnavailableAsync(Exception exception)
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupLeaseAcquired();
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(exception)
            .Verifiable();
        MockRecorder
            .Setup(x =>
                x.TokenRefreshFailedAsync(
                    httpContext,
                    "sub-1",
                    BffTokenFailureReasons.ProviderUnavailable,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync(LeaseKey, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.ProviderUnavailable, result.Reason);
    }

    private void SetupFailedRefresh(BffSessionState state, HttpContext httpContext, string error)
    {
        SetupReload(state);
        SetupLeaseAcquired();
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new TokenResult(new ProtocolError(error, 400)))
            .Verifiable();
        MockRecorder
            .Setup(x =>
                x.TokenRefreshFailedAsync(httpContext, "sub-1", error, CancellationToken.None)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync(LeaseKey, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    private void SetupLeaseLost() =>
        MockStore
            .Setup(x =>
                x.TryAcquireRefreshLeaseAsync(
                    LeaseKey,
                    It.IsAny<DateTimeOffset>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(false)
            .Verifiable();

    private DefaultBffUserTokenService CreateWaitingService()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new BffOptions
            {
                Authority = "https://op.example",
                ClientId = "bff",
                Tokens = { RefreshLeaseDuration = TimeSpan.FromMilliseconds(400) },
            }
        );
        var timeProvider = System.TimeProvider.System;
        var cookies = new BffCookies(options, timeProvider);
        var resolver = new BffSessionResolver(
            MockManager.Object,
            cookies,
            [],
            options,
            timeProvider
        );
        return new DefaultBffUserTokenService(
            resolver,
            MockManager.Object,
            MockStore.Object,
            MockTokenClient.Object,
            new BffClientRegistrationProvider(options),
            new BffRefreshLocks(),
            cookies,
            MockRecorder.Object,
            options,
            timeProvider,
            CreateLooseMock<ILogger<DefaultBffUserTokenService>>().Object
        );
    }

    private void SetupReload(BffSessionState state) =>
        MockManager
            .Setup(x => x.LoadAsync(Handle, CancellationToken.None))
            .ReturnsAsync(state)
            .Verifiable();

    private void SetupLeaseAcquired() =>
        MockStore
            .Setup(x =>
                x.TryAcquireRefreshLeaseAsync(LeaseKey, Now.AddSeconds(40), CancellationToken.None)
            )
            .ReturnsAsync(true)
            .Verifiable();

    #endregion
}
