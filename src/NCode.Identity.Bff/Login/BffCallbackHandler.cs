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
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Jose;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Json;
using NCode.Time;

namespace NCode.Identity.Bff.Login;

/// <summary>
/// Handles <c>GET /bff/callback</c>: binds the returning browser to its login transaction.
/// </summary>
internal class BffCallbackHandler
{
    private const string RefreshTokenExpiresInMember = "refresh_token_expires_in";

    private readonly IOptions<BffOptions> _options;
    private readonly BffClientRegistrationProvider _registrationProvider;
    private readonly BffLoginTransactionService _transactionService;
    private readonly BffCookies _cookies;
    private readonly IProviderMetadataResolver _metadataResolver;
    private readonly ITokenClient _tokenClient;
    private readonly IIdTokenValidator _idTokenValidator;
    private readonly IEnumerable<IBffLoginGate> _gates;
    private readonly IBffSessionManager _sessionManager;
    private readonly IBffAuditRecorder _auditRecorder;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BffCallbackHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BffCallbackHandler"/> class.
    /// </summary>
    public BffCallbackHandler(
        IOptions<BffOptions> options,
        BffClientRegistrationProvider registrationProvider,
        BffLoginTransactionService transactionService,
        BffCookies cookies,
        IProviderMetadataResolver metadataResolver,
        ITokenClient tokenClient,
        IIdTokenValidator idTokenValidator,
        IEnumerable<IBffLoginGate> gates,
        IBffSessionManager sessionManager,
        IBffAuditRecorder auditRecorder,
        TimeProvider timeProvider,
        ILogger<BffCallbackHandler> logger
    )
    {
        _options = options;
        _registrationProvider = registrationProvider;
        _transactionService = transactionService;
        _cookies = cookies;
        _metadataResolver = metadataResolver;
        _tokenClient = tokenClient;
        _idTokenValidator = idTokenValidator;
        _gates = gates;
        _sessionManager = sessionManager;
        _auditRecorder = auditRecorder;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Handles the callback request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    public async Task HandleAsync(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var cancellationToken = httpContext.RequestAborted;

        BffResponses.SetNoStore(httpContext.Response);
        _cookies.DeleteLogin(httpContext.Response);

        var stateValues = request.Query[OpenIdProtocol.Parameters.State];
        var state = stateValues.Count == 1 ? stateValues[0] : null;
        var loginSecret = _cookies.ReadLoginSecret(request);
        var taken = await _transactionService.TakeAsync(state, loginSecret, cancellationToken);
        if (taken.Transaction is not { } transaction)
        {
            await FailAsync(
                httpContext,
                taken.FailureReason ?? BffLoginFailureReasons.UnknownState
            );
            return;
        }

        if (request.Query.ContainsKey(OpenIdProtocol.Parameters.ErrorCode))
        {
            await FailAsync(httpContext, BffLoginFailureReasons.AuthorizationError);
            return;
        }

        var metadata = await _metadataResolver.GetMetadataAsync(
            transaction.Issuer,
            cancellationToken
        );
        var issValues = request.Query[OpenIdProtocol.Parameters.IssuerShort];
        var issMissing = issValues.Count == 0;
        var issMismatch =
            !issMissing
            && (
                issValues.Count != 1
                || !string.Equals(issValues[0], transaction.Issuer, StringComparison.Ordinal)
            );
        if (issMismatch || (issMissing && metadata.AuthorizationResponseIssParameterSupported))
        {
            await FailAsync(httpContext, BffLoginFailureReasons.IssuerMismatch);
            return;
        }

        var codeValues = request.Query[OpenIdProtocol.Parameters.AuthorizationCode];
        var code = codeValues.Count == 1 ? codeValues[0] : null;
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            await FailAsync(httpContext, BffLoginFailureReasons.InvalidResponse);
            return;
        }

        var registration = _registrationProvider.Registration;
        var tokenRequest = new AuthorizationCodeTokenRequest(
            code,
            transaction.RedirectUri,
            transaction.CodeVerifier
        )
        {
            DPoPJwkThumbprint = transaction.DPoPJwkThumbprint,
        };

        TokenResult tokenResult;
        try
        {
            tokenResult = await _tokenClient.RequestTokenAsync(
                registration,
                tokenRequest,
                cancellationToken
            );
        }
        catch (OpenIdProviderException)
        {
            await FailAsync(httpContext, BffLoginFailureReasons.TokenError);
            return;
        }

        if (tokenResult.Response is not { IdToken: { } idToken, AccessToken: { } accessToken })
        {
            await FailAsync(httpContext, BffLoginFailureReasons.TokenError);
            return;
        }

        var validationRequest = new IdTokenValidationRequest
        {
            IdToken = idToken,
            Nonce = transaction.Nonce,
            MaxAge = transaction.MaxAge is { } maxAgeSeconds
                ? TimeSpan.FromSeconds(maxAgeSeconds)
                : null,
            AccessToken = accessToken,
            AuthorizationCode = code,
            State = state,
        };
        var validation = await _idTokenValidator.ValidateAsync(
            registration,
            validationRequest,
            cancellationToken
        );
        if (validation.Identity is not { } identity || !validation.IsValid)
        {
            var reason = string.Equals(
                validation.FailureCode,
                TokenValidationFailureCodes.NonceMismatch,
                StringComparison.Ordinal
            )
                ? BffLoginFailureReasons.NonceMismatch
                : BffLoginFailureReasons.IdTokenInvalid;
            await FailAsync(httpContext, reason);
            return;
        }

        var claims = BffSessionPrincipal.StoreClaims(identity);
        var principal = BffSessionPrincipal.Create(claims, transaction.Issuer);
        var gateContext = new BffLoginGateContext(httpContext, transaction.Issuer, principal);
        foreach (var gate in _gates)
        {
            var allowed = await gate.AllowAsync(gateContext, cancellationToken);
            if (allowed)
                continue;

            await FailAsync(httpContext, BffLoginFailureReasons.RejectedByGate);
            return;
        }

        var subject = identity.FindFirst(JoseClaimNames.Payload.Sub)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            await FailAsync(httpContext, BffLoginFailureReasons.IdTokenInvalid);
            return;
        }

        var response = tokenResult.Response;
        var bffOptions = _options.Value;
        var now = _timeProvider.GetUtcNowWithPrecisionInSeconds();

        var absolute = now + bffOptions.Session.AbsoluteLifetime;
        if (
            response.ExtensionData is { } extensionData
            && extensionData.TryGetValue(RefreshTokenExpiresInMember, out var refreshExpiry)
            && refreshExpiry.TryGetWholeInt32(out var refreshSeconds)
            && refreshSeconds > 0
        )
        {
            var refreshExpiresAt = now + TimeSpan.FromSeconds(refreshSeconds);
            if (refreshExpiresAt < absolute)
                absolute = refreshExpiresAt;
        }

        var idle = now + bffOptions.Session.IdleTimeout;
        if (absolute < idle)
            idle = absolute;

        var sessionMetadata = new BffSessionMetadata
        {
            Frontend = BffDefaults.FrontendName,
            Issuer = transaction.Issuer,
            Subject = subject,
            SessionId = identity.FindFirst(JoseClaimNames.Payload.Sid)?.Value,
            CreatedAt = now,
            LastAccessedAt = now,
            AbsoluteExpiresAt = absolute,
            IdleExpiresAt = idle,
        };

        var expiresAt = response.ExpiresIn is { } expiresIn
            ? now + TimeSpan.FromSeconds(expiresIn)
            : now;
        var payload = new BffSessionPayload
        {
            Claims = [.. claims],
            IdToken = idToken,
            RefreshToken = response.RefreshToken,
            DPoPJwkThumbprint = tokenResult.DPoPJwkThumbprint,
            LogoutGuard = BffKeyDerivation.CreateSecret(),
            AccessTokens =
            [
                new BffStoredAccessToken
                {
                    Requirement = BffTokenRequirementNames.User,
                    Resource = null,
                    Scope = null,
                    Value = accessToken,
                    TokenType = response.TokenType ?? OpenIdProtocol.TokenTypes.Bearer,
                    ExpiresAt = expiresAt,
                    DPoPJwkThumbprint = tokenResult.DPoPJwkThumbprint,
                },
            ],
        };

        var oldHandle = _cookies.ReadSessionHandle(request);
        if (oldHandle is not null)
        {
            var old = await _sessionManager.LoadAsync(oldHandle, cancellationToken);
            if (old is not null)
            {
                await _sessionManager.EndAsync(
                    httpContext,
                    old.StoreKey,
                    old.Metadata,
                    BffSessionEndReasons.Replaced,
                    cancellationToken
                );
            }
        }

        var created = await _sessionManager.CreateAsync(
            sessionMetadata,
            payload,
            cancellationToken
        );
        _cookies.SetSession(httpContext.Response, created.Handle, absolute);
        await _auditRecorder.LoginSucceededAsync(httpContext, subject, cancellationToken);

        var returnUrl = BffNavigation.LocalUrl(request, transaction.ReturnUrl);
        httpContext.Response.Redirect(returnUrl);
    }

    private async ValueTask FailAsync(HttpContext httpContext, string reason)
    {
        _logger.LoginFailed(reason);
        await _auditRecorder.LoginFailedAsync(httpContext, reason, httpContext.RequestAborted);
        var errorUrl = BffNavigation.ErrorUrl(httpContext.Request, _options.Value, reason);
        httpContext.Response.Redirect(errorUrl);
    }
}
