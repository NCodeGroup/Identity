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

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Jose;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Login;

public class BffCallbackHandlerTests : BaseTests
{
    private const string Issuer = "https://op.example.com";
    private const string State = "state-value";
    private const string Nonce = "nonce-value";

    // 32 random bytes in unpadded base64url.
    private static readonly string LoginSecret = new('a', 43);

    private readonly Mock<BffLoginTransactionService> _transactionService;
    private readonly Mock<IProviderMetadataResolver> _metadataResolver;
    private readonly Mock<IBffAuditRecorder> _auditRecorder;
    private readonly Mock<ITokenClient> _tokenClient;
    private readonly Mock<IIdTokenValidator> _idTokenValidator;
    private readonly Mock<IBffSessionManager> _sessionManager;
    private readonly MutableTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
    );
    private readonly List<IBffLoginGate> _gates = [];

    public BffCallbackHandlerTests()
    {
        _transactionService = CreateStrictMock<BffLoginTransactionService>(
            CreateStrictMock<IBffSessionStore>().Object
        );
        _metadataResolver = CreateStrictMock<IProviderMetadataResolver>();
        _auditRecorder = CreateStrictMock<IBffAuditRecorder>();
        _tokenClient = CreateStrictMock<ITokenClient>();
        _idTokenValidator = CreateStrictMock<IIdTokenValidator>();
        _sessionManager = CreateStrictMock<IBffSessionManager>();
    }

    private BffCallbackHandler CreateHandler()
    {
        var options = Microsoft.Extensions.Options.Options.Create(CreateOptions());
        var timeProvider = _timeProvider;
        return new BffCallbackHandler(
            options,
            new BffClientRegistrationProvider(options),
            _transactionService.Object,
            new BffCookies(options, timeProvider),
            _metadataResolver.Object,
            _tokenClient.Object,
            _idTokenValidator.Object,
            _gates,
            _sessionManager.Object,
            _auditRecorder.Object,
            timeProvider,
            CreateLooseMock<ILogger<BffCallbackHandler>>().Object
        );
    }

    private static BffOptions CreateOptions() => new() { Authority = Issuer, ClientId = "bff" };

    private static DefaultHttpContext CreateHttpContext(string query, string? oldHandle = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("app.example.com");
        httpContext.Request.QueryString = new QueryString(query);
        var cookie = $"{BffDefaults.LoginCookieName}={LoginSecret}";
        if (oldHandle is not null)
            cookie += $"; {BffDefaults.SessionCookieName}={oldHandle}";
        httpContext.Request.Headers.Cookie = cookie;
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private void SetupTake(string? state, BffLoginTransactionResult result) =>
        _transactionService
            .Setup(x => x.TakeAsync(state, LoginSecret, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupTransaction(bool issSupported)
    {
        var transaction = new BffLoginTransaction
        {
            Issuer = Issuer,
            RedirectUri = "https://app.example.com/bff/callback",
            CodeVerifier = "verifier",
            Nonce = Nonce,
            ReturnUrl = "/home",
        };
        SetupTake(State, new BffLoginTransactionResult(transaction, null));
        var metadata = new OpenIdProviderMetadata
        {
            Issuer = Issuer,
            AuthorizationResponseIssParameterSupported = issSupported,
        };
        _metadataResolver
            .Setup(x => x.GetMetadataAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata)
            .Verifiable();
    }

    private void SetupAudit(string reason) =>
        _auditRecorder
            .Setup(x =>
                x.LoginFailedAsync(It.IsAny<HttpContext>(), reason, It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private async Task AssertFailsAsync(DefaultHttpContext httpContext, string reason)
    {
        await CreateHandler().HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal($"/?error={reason}", httpContext.Response.Headers.Location.ToString());
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_NoState_FailsWithUnknownState()
    {
        SetupTake(null, new BffLoginTransactionResult(null, BffLoginFailureReasons.UnknownState));
        SetupAudit(BffLoginFailureReasons.UnknownState);

        await AssertFailsAsync(CreateHttpContext("?code=abc"), BffLoginFailureReasons.UnknownState);
    }

    [Fact]
    public async Task HandleAsync_NoTransaction_FailsWithUnknownState()
    {
        SetupTake(State, new BffLoginTransactionResult(null, BffLoginFailureReasons.UnknownState));
        SetupAudit(BffLoginFailureReasons.UnknownState);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.UnknownState
        );
    }

    [Fact]
    public async Task HandleAsync_InitiatorMismatch_FailsWithInitiatorMismatch()
    {
        SetupTake(
            State,
            new BffLoginTransactionResult(null, BffLoginFailureReasons.InitiatorMismatch)
        );
        SetupAudit(BffLoginFailureReasons.InitiatorMismatch);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.InitiatorMismatch
        );
    }

    [Fact]
    public async Task HandleAsync_ErrorResponse_FailsWithAuthorizationError()
    {
        SetupTake(State, new BffLoginTransactionResult(new BffLoginTransaction(), null));
        SetupAudit(BffLoginFailureReasons.AuthorizationError);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&error=access_denied"),
            BffLoginFailureReasons.AuthorizationError
        );
    }

    [Fact]
    public async Task HandleAsync_IssuerMismatch_FailsWithIssuerMismatch()
    {
        SetupTransaction(issSupported: true);
        SetupAudit(BffLoginFailureReasons.IssuerMismatch);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc&iss=https%3A%2F%2Fevil.example.com"),
            BffLoginFailureReasons.IssuerMismatch
        );
    }

    [Fact]
    public async Task HandleAsync_MissingIssWhenSupported_FailsWithIssuerMismatch()
    {
        SetupTransaction(issSupported: true);
        SetupAudit(BffLoginFailureReasons.IssuerMismatch);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.IssuerMismatch
        );
    }

    [Fact]
    public async Task HandleAsync_NoCode_FailsWithInvalidResponse()
    {
        SetupTransaction(issSupported: false);
        SetupAudit(BffLoginFailureReasons.InvalidResponse);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}"),
            BffLoginFailureReasons.InvalidResponse
        );
    }

    [Fact]
    public async Task HandleAsync_TokenErrorResult_FailsWithTokenError()
    {
        SetupTransaction(issSupported: false);
        SetupToken(new TokenResult(new ProtocolError(OpenIdProtocol.ErrorCodes.InvalidGrant, 400)));
        SetupAudit(BffLoginFailureReasons.TokenError);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.TokenError
        );
    }

    [Fact]
    public async Task HandleAsync_ProviderException_FailsWithProviderUnavailable()
    {
        SetupTransaction(issSupported: false);
        _tokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new OpenIdProviderException("unavailable"))
            .Verifiable();
        SetupAudit(BffLoginFailureReasons.ProviderUnavailable);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.ProviderUnavailable
        );
    }

    [Fact]
    public async Task HandleAsync_TokenTransportFailure_FailsWithProviderUnavailable()
    {
        SetupTransaction(issSupported: false);
        _tokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new HttpRequestException("down"))
            .Verifiable();
        SetupAudit(BffLoginFailureReasons.ProviderUnavailable);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.ProviderUnavailable
        );
    }

    [Fact]
    public async Task HandleAsync_MetadataUnavailable_FailsWithProviderUnavailable()
    {
        var transaction = new BffLoginTransaction { Issuer = Issuer };
        SetupTake(State, new BffLoginTransactionResult(transaction, null));
        _metadataResolver
            .Setup(x => x.GetMetadataAsync(Issuer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OpenIdProviderException("unavailable"))
            .Verifiable();
        SetupAudit(BffLoginFailureReasons.ProviderUnavailable);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.ProviderUnavailable
        );
    }

    [Fact]
    public async Task HandleAsync_IdTokenKeysUnavailable_FailsWithProviderUnavailable()
    {
        SetupTransaction(issSupported: false);
        SetupSuccessfulToken();
        _idTokenValidator
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<IdTokenValidationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OpenIdProviderException("jwks unavailable"))
            .Verifiable();
        SetupAudit(BffLoginFailureReasons.ProviderUnavailable);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.ProviderUnavailable
        );
    }

    [Fact]
    public async Task HandleAsync_NoIdToken_FailsWithTokenError()
    {
        SetupTransaction(issSupported: false);
        SetupToken(new TokenResult(new TokenResponse { AccessToken = "at" }));
        SetupAudit(BffLoginFailureReasons.TokenError);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.TokenError
        );
    }

    [Fact]
    public async Task HandleAsync_NonceMismatch_FailsWithNonceMismatch()
    {
        SetupTransaction(issSupported: false);
        SetupSuccessfulToken();
        SetupValidation(
            new IdTokenValidationResult(
                TokenValidationFailureCodes.NonceMismatch,
                "The nonce is wrong."
            )
        );
        SetupAudit(BffLoginFailureReasons.NonceMismatch);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.NonceMismatch
        );
    }

    [Fact]
    public async Task HandleAsync_InvalidIdToken_FailsWithIdTokenInvalid()
    {
        SetupTransaction(issSupported: false);
        SetupSuccessfulToken();
        SetupValidation(
            new IdTokenValidationResult(
                TokenValidationFailureCodes.InvalidSignature,
                "The signature is wrong."
            )
        );
        SetupAudit(BffLoginFailureReasons.IdTokenInvalid);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.IdTokenInvalid
        );
    }

    [Fact]
    public async Task HandleAsync_GateRejects_FailsWithRejectedByGate()
    {
        SetupTransaction(issSupported: false);
        SetupSuccessfulToken();
        var identity = new ClaimsIdentity([new Claim(JoseClaimNames.Payload.Sub, "user-1")]);
        SetupValidation(new IdTokenValidationResult(default(JsonElement)) { Identity = identity });
        var gate = CreateStrictMock<IBffLoginGate>();
        gate.Setup(x =>
                x.AllowAsync(It.IsAny<BffLoginGateContext>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false)
            .Verifiable();
        _gates.Add(gate.Object);
        SetupAudit(BffLoginFailureReasons.RejectedByGate);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.RejectedByGate
        );
    }

    private BffSessionMetadata? _createdMetadata;
    private BffSessionPayload? _createdPayload;

    private void SetupSuccess(TokenResponse response)
    {
        SetupTransaction(issSupported: false);
        SetupToken(new TokenResult(response) { DPoPJwkThumbprint = "jkt" });
        var identity = new ClaimsIdentity([
            new Claim(JoseClaimNames.Payload.Sub, "user-1"),
            new Claim(JoseClaimNames.Payload.Sid, "sid-1"),
        ]);
        SetupValidation(new IdTokenValidationResult(default(JsonElement)) { Identity = identity });
        _sessionManager
            .Setup(x =>
                x.CreateAsync(
                    It.IsAny<BffSessionMetadata>(),
                    It.IsAny<BffSessionPayload>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<BffSessionMetadata, BffSessionPayload, CancellationToken>(
                (metadata, payload, _) =>
                {
                    _createdMetadata = metadata;
                    _createdPayload = payload;
                }
            )
            .ReturnsAsync(
                new BffSessionState(
                    new string('h', 43),
                    "key",
                    1,
                    new BffSessionMetadata(),
                    new BffSessionPayload()
                )
            )
            .Verifiable();
        _auditRecorder
            .Setup(x =>
                x.LoginSucceededAsync(
                    It.IsAny<HttpContext>(),
                    "user-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    [Fact]
    public async Task HandleAsync_Success_CreatesSessionSetsCookieAndRedirects()
    {
        var response = new TokenResponse
        {
            AccessToken = "at",
            IdToken = "idt",
            RefreshToken = "rt",
            ExpiresIn = 300,
        };
        SetupSuccess(response);
        var httpContext = CreateHttpContext($"?state={State}&code=abc");

        await CreateHandler().HandleAsync(httpContext);

        var now = _timeProvider.GetUtcNow();
        var metadata = Assert.IsType<BffSessionMetadata>(_createdMetadata);
        Assert.Equal(BffDefaults.FrontendName, metadata.Frontend);
        Assert.Equal(Issuer, metadata.Issuer);
        Assert.Equal("user-1", metadata.Subject);
        Assert.Equal("sid-1", metadata.SessionId);
        Assert.Equal(now, metadata.CreatedAt);
        Assert.Equal(now + CreateOptions().Session.AbsoluteLifetime, metadata.AbsoluteExpiresAt);
        Assert.Equal(now + CreateOptions().Session.IdleTimeout, metadata.IdleExpiresAt);
        var payload = Assert.IsType<BffSessionPayload>(_createdPayload);
        Assert.Equal("idt", payload.IdToken);
        Assert.Equal("rt", payload.RefreshToken);
        Assert.Equal("jkt", payload.DPoPJwkThumbprint);
        Assert.NotEmpty(payload.LogoutGuard);
        var token = Assert.Single(payload.AccessTokens);
        Assert.Equal("at", token.Value);
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, token.TokenType);
        Assert.Equal(now + TimeSpan.FromSeconds(300), token.ExpiresAt);
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/home", httpContext.Response.Headers.Location.ToString());
        Assert.Contains(
            BffDefaults.SessionCookieName,
            httpContext.Response.Headers.SetCookie.ToString()
        );
    }

    [Fact]
    public async Task HandleAsync_RefreshTokenExpiresIn_CapsAbsoluteExpiry()
    {
        var response = new TokenResponse
        {
            AccessToken = "at",
            IdToken = "idt",
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["refresh_token_expires_in"] = JsonSerializer.SerializeToElement(600),
            },
        };
        SetupSuccess(response);

        await CreateHandler().HandleAsync(CreateHttpContext($"?state={State}&code=abc"));

        var metadata = Assert.IsType<BffSessionMetadata>(_createdMetadata);
        var expected = _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(600);
        Assert.Equal(expected, metadata.AbsoluteExpiresAt);
        Assert.Equal(expected, metadata.IdleExpiresAt);
    }

    [Fact]
    public async Task HandleAsync_OldSession_EndsItAsReplaced()
    {
        SetupSuccess(new TokenResponse { AccessToken = "at", IdToken = "idt" });
        var oldHandle = new string('o', 43);
        var oldMetadata = new BffSessionMetadata { Subject = "old" };
        _sessionManager
            .Setup(x => x.LoadAsync(oldHandle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new BffSessionState(oldHandle, "old-key", 1, oldMetadata, new BffSessionPayload())
            )
            .Verifiable();
        _sessionManager
            .Setup(x =>
                x.EndAsync(
                    It.IsAny<HttpContext?>(),
                    "old-key",
                    oldMetadata,
                    BffSessionEndReasons.Replaced,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateHandler().HandleAsync(CreateHttpContext($"?state={State}&code=abc", oldHandle));
    }

    #endregion

    #region StoreClaims Tests

    [Fact]
    public void StoreClaims_WithNonceAndEmail_DropsNonceAndKeepsEmail()
    {
        var identity = new ClaimsIdentity([
            new Claim(JoseClaimNames.Payload.Nonce, Nonce),
            new Claim(JoseClaimNames.Payload.Email, "user@example.com"),
        ]);

        var result = BffSessionPrincipal.StoreClaims(identity);

        var stored = Assert.Single(result);
        Assert.Equal(JoseClaimNames.Payload.Email, stored.Type);
        Assert.Equal("user@example.com", stored.Value);
    }

    #endregion

    private void SetupToken(TokenResult result) =>
        _tokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupSuccessfulToken() =>
        SetupToken(new TokenResult(new TokenResponse { AccessToken = "at", IdToken = "idt" }));

    private void SetupValidation(IdTokenValidationResult result) =>
        _idTokenValidator
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<ClientRegistration>(),
                    It.Is<IdTokenValidationRequest>(r => r.Nonce == Nonce && r.State == State),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
}
