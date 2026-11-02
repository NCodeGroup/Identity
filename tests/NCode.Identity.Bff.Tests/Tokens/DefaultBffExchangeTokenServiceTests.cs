// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
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

    private Mock<IBffSessionManager> MockManager { get; }
    private Mock<IBffUserTokenService> MockUserTokens { get; }
    private Mock<ITokenClient> MockTokenClient { get; }
    private DefaultBffExchangeTokenService Service { get; }

    public DefaultBffExchangeTokenServiceTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new BffOptions { Authority = "https://op.example", ClientId = "bff" }
        );
        var timeProvider = new FakeTimeProvider(Now);
        MockManager = CreateStrictMock<IBffSessionManager>();
        MockUserTokens = CreateStrictMock<IBffUserTokenService>();
        MockTokenClient = CreateStrictMock<ITokenClient>();
        var resolver = new BffSessionResolver(
            MockManager.Object,
            new BffCookies(options, timeProvider),
            [],
            options,
            timeProvider
        );
        var grantClient = new BffGrantClient(
            MockTokenClient.Object,
            new BffClientRegistrationProvider(options),
            timeProvider,
            CreateLooseMock<ILogger<BffGrantClient>>().Object
        );
        Service = new DefaultBffExchangeTokenService(
            resolver,
            MockManager.Object,
            MockUserTokens.Object,
            grantClient,
            options,
            timeProvider
        );
    }

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
            .Setup(x => x.GetAccessTokenAsync(httpContext, null, null, CancellationToken.None))
            .ReturnsAsync(result)
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
            CreateHttpContext(null),
            Resource,
            "read",
            CancellationToken.None
        );

        Assert.Equal(401, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.SessionRequired, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_FreshCachedToken_ReturnsItWithoutExchanging()
    {
        var state = CreateState(CreateExchanged(Now.AddMinutes(5)));

        var result = await Service.GetAccessTokenAsync(
            CreateHttpContext(state),
            Resource,
            "read",
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
            CreateHttpContext(state),
            Resource,
            "read",
            CancellationToken.None
        );

        Assert.Equal("exchanged", result.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_SessionEndedWhileWaiting_ReturnsSessionRequired()
    {
        SetupReload(null);

        var result = await Service.GetAccessTokenAsync(
            CreateHttpContext(CreateState()),
            Resource,
            "read",
            CancellationToken.None
        );

        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UserTokenFails_ReturnsThatFailure()
    {
        var state = CreateState();
        var httpContext = CreateHttpContext(state);
        SetupReload(state);
        var failure = BffTokenResult.Failure(401, BffTokenFailureReasons.RefreshFailed);
        SetupSubject(httpContext, failure);

        var result = await Service.GetAccessTokenAsync(
            httpContext,
            Resource,
            "read",
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
            httpContext,
            Resource,
            "read",
            CancellationToken.None
        );

        Assert.Equal(401, result.StatusCode);
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
            httpContext,
            Resource,
            "read",
            CancellationToken.None
        );

        Assert.Equal(502, result.StatusCode);
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
            httpContext,
            Resource,
            "read",
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
            httpContext,
            Resource,
            "read",
            CancellationToken.None
        );

        Assert.Equal(401, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.SessionRequired, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullHttpContext_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Service.GetAccessTokenAsync(null!, Resource, "read", CancellationToken.None).AsTask()
        );
    }

    #endregion
}
