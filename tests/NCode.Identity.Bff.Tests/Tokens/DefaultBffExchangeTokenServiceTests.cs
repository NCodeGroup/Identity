// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class DefaultBffExchangeTokenServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static readonly string Handle = new('a', 43);

    private const string Resource = "https://downstream.example";

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    private static BffTokenRequest ExchangeRequest { get; } =
        new(BffTokenRequirementNames.Exchange) { Resource = Resource, Scope = "read" };

    private Mock<IBffSessionManager> MockManager { get; }
    private Mock<IBffUserTokenService> MockUserTokens { get; }
    private Mock<ITokenClient> MockTokenClient { get; }
    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private BffCookies Cookies { get; }
    private BffFrontendCatalog Frontends { get; }
    private BffSessionResolver Resolver { get; }
    private BffRefreshLocks Locks { get; } = new();
    private DefaultBffExchangeTokenService Service { get; }

    public DefaultBffExchangeTokenServiceTests()
    {
        MockManager = CreateStrictMock<IBffSessionManager>();
        MockUserTokens = CreateStrictMock<IBffUserTokenService>();
        MockTokenClient = CreateStrictMock<ITokenClient>();
        Cookies = new BffCookies(TimeProvider);
        Frontends = BffTestFrontends.CreateCatalog();
        Resolver = new BffSessionResolver(MockManager.Object, Cookies, [], Frontends, TimeProvider);
        Service = CreateService(TimeSpan.FromSeconds(40));
    }

    private DefaultBffExchangeTokenService CreateService(TimeSpan refreshLeaseDuration)
    {
        var frontends = BffTestFrontends.CreateCatalog(
            BffTestFrontends.CreateBffOptions(x =>
                x.Tokens.RefreshLeaseDuration = refreshLeaseDuration
            )
        );
        var grantClient = new BffGrantClient(
            MockTokenClient.Object,
            TimeProvider,
            CreateLooseMock<ILogger<BffGrantClient>>().Object
        );
        return new DefaultBffExchangeTokenService(
            MockManager.Object,
            MockUserTokens.Object,
            grantClient,
            Locks,
            frontends,
            TimeProvider
        );
    }

    private BffRequestTokenContext CreateScope(HttpContext httpContext) =>
        new(httpContext, Frontends, Resolver, Cookies);

    private static DefaultHttpContext CreateHttpContext(BffSessionState? session)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set(new BffRequestFeature { IsResolved = true, Session = session });
        return httpContext;
    }

    private static BffSessionState CreateState(params BffStoredAccessToken[] accessTokens) =>
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
                IdleExpiresAt = Now.AddMinutes(59),
            },
            new BffSessionPayload
            {
                IdToken = "id-token",
                LogoutGuard = "guard",
                DPoPJwkThumbprint = "jkt-1",
                AccessTokens = [.. accessTokens],
            }
        );

    private static BffStoredAccessToken CreateExchanged(DateTimeOffset expiresAt) =>
        new()
        {
            Requirement = BffTokenRequirementNames.Exchange,
            Resource = Resource,
            Scope = "read",
            Value = "exchanged",
            TokenType = "DPoP",
            ExpiresAt = expiresAt,
            DPoPJwkThumbprint = "jkt-1",
        };

    private void SetupReload(BffSessionState? state) =>
        MockManager
            .Setup(x => x.LoadAsync(Handle, CancellationToken.None))
            .ReturnsAsync(state)
            .Verifiable();

    private void SetupSubject(HttpContext httpContext, BffTokenResult result) =>
        MockUserTokens
            .Setup(x =>
                x.GetAccessTokenAsync(
                    It.Is<IBffSessionScope>(s => s.HttpContext == httpContext),
                    IsSubjectRequest(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    // the subject token is the user's token for the provider's default resource and scope
    private static BffTokenRequest IsSubjectRequest() =>
        It.Is<BffTokenRequest>(r =>
            r.Requirement == BffTokenRequirementNames.User && r.Resource == null && r.Scope == null
        );

    private TaskCompletionSource<BffTokenResult> SetupBlockedSubject(Times times)
    {
        var release = new TaskCompletionSource<BffTokenResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        MockUserTokens
            .Setup(x =>
                x.GetAccessTokenAsync(
                    It.IsAny<IBffSessionScope>(),
                    IsSubjectRequest(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (IBffSessionScope _, BffTokenRequest _, CancellationToken cancellationToken) =>
                    new ValueTask<BffTokenResult>(release.Task.WaitAsync(cancellationToken))
            )
            .Verifiable(times);
        return release;
    }

    private void SetupWrite(BffSessionState state) =>
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

    private void SetupExchange(TokenResult result) =>
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.Is<TokenRequest>(r =>
                        r is TokenExchangeTokenRequest
                        && ((TokenExchangeTokenRequest)r).SubjectToken == "user-access"
                        && ((TokenExchangeTokenRequest)r).SubjectTokenType
                            == OpenIdProtocol.TokenTypeIdentifiers.AccessToken
                        && r.Resources.SequenceEqual(new[] { Resource })
                        && r.Scope == "read"
                        && r.DPoPJwkThumbprint == "jkt-1"
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private static BffTokenResult UserToken() =>
        BffTokenResult.Success(new BffAccessToken("user-access", "DPoP", Now.AddMinutes(5)));

    #region GetAccessTokenAsync Tests

    [Fact]
    public async Task GetAccessTokenAsync_NoSession_ReturnsSessionRequired()
    {
        var result = await Service.GetAccessTokenAsync(
            CreateScope(CreateHttpContext(null)),
            ExchangeRequest,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.SessionRequired, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_FreshCachedToken_ReturnsItWithoutExchanging()
    {
        var state = CreateState(CreateExchanged(Now.AddMinutes(5)));

        var result = await Service.GetAccessTokenAsync(
            CreateScope(CreateHttpContext(state)),
            ExchangeRequest,
            CancellationToken.None
        );

        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal("exchanged", token.Value);
        Assert.Equal("DPoP", token.TokenType);
        Assert.Equal("jkt-1", token.DPoPJwkThumbprint);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReloadedSessionHasFreshToken_ReturnsItWithoutExchanging()
    {
        var state = CreateState();
        SetupReload(CreateState(CreateExchanged(Now.AddMinutes(5))));

        var result = await Service.GetAccessTokenAsync(
            CreateScope(CreateHttpContext(state)),
            ExchangeRequest,
            CancellationToken.None
        );

        Assert.Equal("exchanged", result.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_SessionEndedWhileWaiting_ReturnsSessionRequired()
    {
        SetupReload(null);

        var result = await Service.GetAccessTokenAsync(
            CreateScope(CreateHttpContext(CreateState())),
            ExchangeRequest,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UserTokenFails_ReturnsThatFailure()
    {
        var state = CreateState();
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        var failure = BffTokenResult.Failure(
            StatusCodes.Status401Unauthorized,
            BffTokenFailureReasons.RefreshFailed
        );
        SetupSubject(httpContext, failure);

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            ExchangeRequest,
            CancellationToken.None
        );

        Assert.Same(failure, result);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UserTokenMissing_ReturnsSessionRequired()
    {
        var state = CreateState();
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupSubject(httpContext, BffTokenResult.Success(null));

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            ExchangeRequest,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.SessionRequired, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ExchangeRefused_ReturnsProviderError()
    {
        var state = CreateState();
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupSubject(httpContext, UserToken());
        SetupExchange(new TokenResult(new ProtocolError("invalid_target", 400)));

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            ExchangeRequest,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.ProviderError, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Exchanged_CachesTokenInSession()
    {
        var state = CreateState(CreateExchanged(Now.AddSeconds(30)));
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupSubject(httpContext, UserToken());
        SetupExchange(
            new TokenResult(
                new TokenResponse
                {
                    AccessToken = "new-exchanged",
                    TokenType = "DPoP",
                    ExpiresIn = 300,
                }
            )
            {
                DPoPJwkThumbprint = "jkt-1",
            }
        );
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

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            ExchangeRequest,
            CancellationToken.None
        );

        Assert.Equal("new-exchanged", result.Token?.Value);
        var stored = Assert.Single(Assert.IsType<BffSessionState>(written).Payload.AccessTokens);
        Assert.Equal(BffTokenRequirementNames.Exchange, stored.Requirement);
        Assert.Equal(Resource, stored.Resource);
        Assert.Equal("read", stored.Scope);
        Assert.Equal("new-exchanged", stored.Value);
        Assert.Equal(Now.AddSeconds(300), stored.ExpiresAt);
        Assert.Equal("jkt-1", stored.DPoPJwkThumbprint);
    }

    [Fact]
    public async Task GetAccessTokenAsync_SessionEndedBeforeWrite_ReturnsSessionRequired()
    {
        var state = CreateState();
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        SetupSubject(httpContext, UserToken());
        SetupExchange(
            new TokenResult(new TokenResponse { AccessToken = "new-exchanged", ExpiresIn = 300 })
        );
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync((BffSessionState?)null)
            .Verifiable();

        var result = await Service.GetAccessTokenAsync(
            CreateScope(httpContext),
            ExchangeRequest,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.SessionRequired, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_LockHeldPastLeaseDuration_ReturnsRefreshWaitTimeout()
    {
        var service = CreateService(TimeSpan.FromMilliseconds(50));
        var state = CreateState();
        SetupReload(state);
        var release = SetupBlockedSubject(Times.Once());
        var first = service
            .GetAccessTokenAsync(
                CreateScope(CreateHttpContext(state)),
                ExchangeRequest,
                CancellationToken.None
            )
            .AsTask();

        var second = await service.GetAccessTokenAsync(
            CreateScope(CreateHttpContext(state)),
            ExchangeRequest,
            CancellationToken.None
        );

        Assert.False(second.IsSuccess);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, second.StatusCode);
        Assert.Equal(BffTokenFailureReasons.RefreshWaitTimeout, second.Reason);

        var failure = BffTokenResult.Failure(
            StatusCodes.Status401Unauthorized,
            BffTokenFailureReasons.RefreshFailed
        );
        release.SetResult(failure);

        Assert.Same(failure, await first.WaitAsync(WaitTimeout));
        Assert.Equal(0, Locks.Count);
    }

    [Fact]
    public async Task GetAccessTokenAsync_HolderCanceled_NextCallerExchangesItself()
    {
        var state = CreateState();
        using var cts = new CancellationTokenSource();
        SetupReload(state);
        MockManager.Setup(x => x.LoadAsync(Handle, cts.Token)).ReturnsAsync(state).Verifiable();
        var release = SetupBlockedSubject(Times.Exactly(2));
        SetupExchange(
            new TokenResult(new TokenResponse { AccessToken = "new-exchanged", ExpiresIn = 300 })
            {
                DPoPJwkThumbprint = "jkt-1",
            }
        );
        SetupWrite(state);
        var first = Service
            .GetAccessTokenAsync(CreateScope(CreateHttpContext(state)), ExchangeRequest, cts.Token)
            .AsTask();
        var second = Service
            .GetAccessTokenAsync(
                CreateScope(CreateHttpContext(state)),
                ExchangeRequest,
                CancellationToken.None
            )
            .AsTask();

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
        Assert.False(second.IsCompleted);

        release.SetResult(UserToken());
        var result = await second.WaitAsync(WaitTimeout);

        Assert.Equal("new-exchanged", result.Token?.Value);
        Assert.Equal(0, Locks.Count);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Service
                .GetAccessTokenAsync(
                    CreateScope(CreateHttpContext(null)),
                    null!,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullScope_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Service.GetAccessTokenAsync(null!, ExchangeRequest, CancellationToken.None).AsTask()
        );
    }

    #endregion
}
