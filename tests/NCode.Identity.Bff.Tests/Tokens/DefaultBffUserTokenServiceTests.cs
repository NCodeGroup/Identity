// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Diagnostics;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
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

    private static readonly BffRefreshLease Lease = new(LeaseKey, "owner-1");

    // the wall-clock time a fake-clock call may take before the test gives up
    private static readonly TimeSpan AdvanceDeadline = TimeSpan.FromSeconds(30);

    private static BffTokenRequest UserRequest { get; } = new(BffTokenRequirementNames.User);

    private Mock<IBffSessionManager> MockManager { get; }
    private Mock<IBffSessionStore> MockStore { get; }
    private Mock<ITokenClient> MockTokenClient { get; }
    private Mock<IBffAuditRecorder> MockRecorder { get; }
    private BffCookies Cookies { get; }
    private BffFrontendCatalog Frontends { get; }
    private BffSessionResolver Resolver { get; }
    private FakeTimeProvider TimeProvider { get; }
    private ServiceProvider MetricsProvider { get; }
    private BffMetrics Metrics { get; }
    private MetricCollector<double> RefreshCollector { get; }
    private ActivityListener Listener { get; }
    private List<Activity> StoppedRefreshes { get; } = [];
    private Activity Parent { get; }
    private DefaultBffUserTokenService Service { get; }

    public DefaultBffUserTokenServiceTests()
    {
        MockManager = CreateStrictMock<IBffSessionManager>();
        MetricsProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = MetricsProvider.GetRequiredService<IMeterFactory>();
        Metrics = new BffMetrics(meterFactory);
        RefreshCollector = new MetricCollector<double>(
            meterFactory,
            BffTelemetry.MeterName,
            BffMetrics.TokenRefreshDurationName
        );
        // The activity source is shared by every test, so only the refresh spans under this test's parent are kept.
        Parent = new Activity(nameof(DefaultBffUserTokenServiceTests)).Start();
        var parentSpanId = Parent.SpanId;
        Listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BffTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (
                    activity.ParentSpanId != parentSpanId
                    || activity.OperationName != BffActivities.TokenRefresh
                )
                    return;

                lock (StoppedRefreshes)
                    StoppedRefreshes.Add(activity);
            },
        };
        ActivitySource.AddActivityListener(Listener);
        var timeProvider = new FakeTimeProvider(Now);
        TimeProvider = timeProvider;
        Cookies = new BffCookies(timeProvider);
        Frontends = BffTestFrontends.CreateCatalog();
        Resolver = new BffSessionResolver(MockManager.Object, Cookies, [], Frontends, timeProvider);
        MockStore = CreateStrictMock<IBffSessionStore>();
        MockTokenClient = CreateStrictMock<ITokenClient>();
        var mockRecorder = CreateStrictMock<IBffAuditRecorder>();
        MockRecorder = mockRecorder;
        Service = new DefaultBffUserTokenService(
            MockManager.Object,
            new BffInstrumentedSessionStore(MockStore.Object, Metrics),
            MockTokenClient.Object,
            new BffRefreshLocks(),
            Metrics,
            mockRecorder.Object,
            Frontends,
            timeProvider,
            CreateLooseMock<ILogger<DefaultBffUserTokenService>>().Object
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Listener.Dispose();
            Parent.Dispose();
            RefreshCollector.Dispose();
            MetricsProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AssertRefreshRecorded(string result, string? errorType)
    {
        var measurement = Assert.Single(RefreshCollector.GetMeasurementSnapshot());
        Assert.Equal("s", RefreshCollector.Instrument?.Unit);
        Assert.True(measurement.Value >= 0);
        Assert.Equal(result, measurement.Tags[BffMetrics.RefreshResultAttribute]);
        if (errorType is null)
            Assert.False(measurement.Tags.ContainsKey(BffMetrics.ErrorTypeAttribute));
        else
            Assert.Equal(errorType, measurement.Tags[BffMetrics.ErrorTypeAttribute]);

        var activity = Assert.Single(StoppedRefreshes);
        Assert.Equal(result, activity.GetTagItem(BffMetrics.RefreshResultAttribute));
        Assert.Equal(errorType, activity.GetTagItem(BffMetrics.ErrorTypeAttribute));
        Assert.Equal(
            errorType is null ? ActivityStatusCode.Unset : ActivityStatusCode.Error,
            activity.Status
        );
    }

    private BffRequestTokenContext CreateScope(HttpContext httpContext) =>
        new(httpContext, Frontends, Resolver, Cookies);

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
                Frontend = BffTestFrontends.Name,
                Authority = BffTestFrontends.Authority,
                ClientId = BffTestFrontends.ClientId,
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
            CreateScope(httpContext),
            UserRequest,
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
            CreateScope(httpContext),
            UserRequest,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal("access-token", token.Value);
        Assert.Equal("DPoP", token.TokenType);
        Assert.Equal(cached.ExpiresAt, token.ExpiresAt);
        Assert.Equal("jkt-1", token.DPoPJwkThumbprint);
        // A fresh cached token is not a refresh, so nothing is measured or traced.
        Assert.Empty(RefreshCollector.GetMeasurementSnapshot());
        Assert.Empty(StoppedRefreshes);
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
            CreateScope(httpContext),
            UserRequest,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.RefreshFailed, result.Reason);
        Assert.Contains(
            httpContext.Response.Headers.SetCookie,
            x => x is not null && x.Contains(BffTestFrontends.SessionCookieName)
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
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            CreateRequest(resource),
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
        AssertRefreshRecorded(BffMetrics.RefreshedResult, errorType: null);
    }

    [Fact]
    public async Task GetAccessTokenAsync_CallerCancelsAfterWinningLease_StillWritesRotatedTokens()
    {
        using var cts = new CancellationTokenSource();
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();
        MockStore
            .Setup(x =>
                x.TryAcquireRefreshLeaseAsync(
                    LeaseKey,
                    Now.AddSeconds(40),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((BffRefreshLease?)Lease)
            .Verifiable();
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
            .Callback(() => cts.Cancel())
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
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            CreateRequest(resource),
            cts.Token
        );

        Assert.True(cts.IsCancellationRequested);
        Assert.True(result.IsSuccess);
        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal("new-access", token.Value);
        Assert.NotNull(written);
        Assert.Equal("rotated-refresh", written.Payload.RefreshToken);
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
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            UserRequest,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(written);
        Assert.Equal("newer-refresh", written.Payload.RefreshToken);
        Assert.Single(written.Payload.AccessTokens);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RefreshOutlastsTheLease_StillReleasesItsOwnLease()
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
                TimeProvider.Advance(TimeSpan.FromSeconds(41));
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
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            UserRequest,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        MockStore.Verify(
            x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None),
            Times.Once
        );
    }

    [Fact]
    public async Task GetAccessTokenAsync_RefreshWithoutTokenType_ReturnsBearerToken()
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
            .ReturnsAsync(
                new TokenResult(new TokenResponse { AccessToken = "new-access", ExpiresIn = 300 })
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
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            UserRequest,
            CancellationToken.None
        );

        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, token.TokenType);
        var stored = Assert.Single(Assert.IsType<BffSessionState>(written).Payload.AccessTokens);
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, stored.TokenType);
    }

    [Fact]
    public async Task GetAccessTokenAsync_LeaseReleaseFails_ReturnsTheRefreshedToken()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupLeaseAcquired();
        var response = new TokenResponse
        {
            AccessToken = "new-access",
            TokenType = "DPoP",
            RefreshToken = "rotated-refresh",
            ExpiresIn = 300,
        };
        var tokenResult = new TokenResult(response);
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(tokenResult)
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
        var releaseFailure = new BffStoreUnavailableException("The release failed.");
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
            .Returns(() => ValueTask.FromException(releaseFailure))
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            UserRequest,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal("new-access", token.Value);
        AssertRefreshRecorded(BffMetrics.RefreshedResult, errorType: null);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WriteBackAndLeaseReleaseFail_ThrowsTheWriteBackFailure()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupLeaseAcquired();
        var response = new TokenResponse
        {
            AccessToken = "new-access",
            TokenType = "DPoP",
            RefreshToken = "rotated-refresh",
            ExpiresIn = 300,
        };
        var tokenResult = new TokenResult(response);
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(tokenResult)
            .Verifiable();
        var writeBackFailure = new BffStoreUnavailableException("The write-back failed.");
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    CancellationToken.None
                )
            )
            .Returns(() => ValueTask.FromException<BffSessionState?>(writeBackFailure))
            .Verifiable();
        var releaseFailure = new BffStoreUnavailableException("The release failed.");
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
            .Returns(() => ValueTask.FromException(releaseFailure))
            .Verifiable();

        var exception = await Assert.ThrowsAsync<BffStoreUnavailableException>(async () =>
            await Service.GetAccessTokenAsync(
                CreateScope(httpContext),
                UserRequest,
                CancellationToken.None
            )
        );

        Assert.Same(writeBackFailure, exception);
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
            CreateScope(httpContext),
            UserRequest,
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
                ExpiresAt = Now.AddHours(1),
            }
        );
        MockManager
            .SetupSequence(x => x.LoadAsync(Handle, CancellationToken.None))
            .ReturnsAsync(state)
            .ReturnsAsync(fresh);
        SetupLeaseLost();

        var result = await AdvanceUntilCompletedAsync(
            service.GetAccessTokenAsync(
                CreateScope(httpContext),
                UserRequest,
                CancellationToken.None
            )
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
        AssertRefreshRecorded(BffMetrics.JoinedResult, errorType: null);
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
            .ReturnsAsync((BffRefreshLease?)new BffRefreshLease(changedLeaseKey, "owner-2"))
            .Verifiable();
        MockStore
            .Setup(x =>
                x.ReleaseRefreshLeaseAsync(
                    new BffRefreshLease(changedLeaseKey, "owner-2"),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
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

        var result = await AdvanceUntilCompletedAsync(
            service.GetAccessTokenAsync(
                CreateScope(httpContext),
                UserRequest,
                CancellationToken.None
            )
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

        var result = await AdvanceUntilCompletedAsync(
            service.GetAccessTokenAsync(
                CreateScope(httpContext),
                UserRequest,
                CancellationToken.None
            )
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
        AssertRefreshRecorded(BffMetrics.FailedResult, BffTokenFailureReasons.RefreshWaitTimeout);
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
            CreateScope(httpContext),
            UserRequest,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.RefreshFailed, result.Reason);
        Assert.Contains(
            httpContext.Response.Headers.SetCookie,
            x => x is not null && x.Contains(BffTestFrontends.SessionCookieName)
        );
        AssertRefreshRecorded(BffMetrics.FailedResult, BffTokenFailureReasons.RefreshFailed);
    }

    [Fact]
    public async Task GetAccessTokenAsync_OtherOAuthError_ReturnsProviderError()
    {
        var state = CreateState("refresh-token");
        var httpContext = CreateHttpContext(state);
        SetupFailedRefresh(state, httpContext, OpenIdProtocol.ErrorCodes.InvalidScope);

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            UserRequest,
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
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            UserRequest,
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
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            UserRequest,
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
            .Setup(x => x.ReleaseRefreshLeaseAsync(Lease, CancellationToken.None))
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
            .ReturnsAsync((BffRefreshLease?)null)
            .Verifiable();

    private static BffTokenRequest CreateRequest(string resource) =>
        new(BffTokenRequirementNames.User) { Resource = resource, Scope = "read" };

    // The backoff waits on the fake clock: advance it in small steps until the call completes. The
    // continuations run on the thread pool, so a step count says nothing about progress; a wall-clock
    // deadline turns a call that never completes into a failure instead of a hung test run.
    private async Task<BffTokenResult> AdvanceUntilCompletedAsync(ValueTask<BffTokenResult> call)
    {
        var task = call.AsTask();
        var started = Stopwatch.GetTimestamp();
        while (!task.IsCompleted)
        {
            if (Stopwatch.GetElapsedTime(started) > AdvanceDeadline)
                Assert.Fail($"The call did not complete within {AdvanceDeadline}.");

            TimeProvider.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Yield();
        }

        return await task;
    }

    private DefaultBffUserTokenService CreateWaitingService()
    {
        var frontends = BffTestFrontends.CreateCatalog(
            BffTestFrontends.CreateBffOptions(x =>
                x.Tokens.RefreshLeaseDuration = TimeSpan.FromMilliseconds(400)
            )
        );
        var timeProvider = TimeProvider;
        return new DefaultBffUserTokenService(
            MockManager.Object,
            new BffInstrumentedSessionStore(MockStore.Object, Metrics),
            MockTokenClient.Object,
            new BffRefreshLocks(),
            Metrics,
            MockRecorder.Object,
            frontends,
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
            .ReturnsAsync((BffRefreshLease?)Lease)
            .Verifiable();

    #endregion
}
