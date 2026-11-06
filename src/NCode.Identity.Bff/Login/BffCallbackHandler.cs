// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.AspNetCore;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Jose;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Time;

namespace NCode.Identity.Bff.Login;

/// <summary>
/// Handles <c>GET /bff/callback</c>: binds the returning browser to its login transaction.
/// </summary>
internal class BffCallbackHandler
{
    private readonly BffFrontendCatalog _frontends;
    private readonly BffLoginTransactionService _transactionService;
    private readonly BffCookies _cookies;
    private readonly IProviderMetadataResolver _metadataResolver;
    private readonly ITokenClient _tokenClient;
    private readonly IIdTokenValidator _idTokenValidator;
    private readonly IUserInfoClient _userInfoClient;
    private readonly IEnumerable<IBffLoginGate> _gates;
    private readonly IEnumerable<IBffSessionClaimContributor> _claimContributors;
    private readonly IBffSessionManager _sessionManager;
    private readonly IBffAuditRecorder _auditRecorder;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BffCallbackHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BffCallbackHandler"/> class.
    /// </summary>
    public BffCallbackHandler(
        BffFrontendCatalog frontends,
        BffLoginTransactionService transactionService,
        BffCookies cookies,
        IProviderMetadataResolver metadataResolver,
        ITokenClient tokenClient,
        IIdTokenValidator idTokenValidator,
        IUserInfoClient userInfoClient,
        IEnumerable<IBffLoginGate> gates,
        IEnumerable<IBffSessionClaimContributor> claimContributors,
        IBffSessionManager sessionManager,
        IBffAuditRecorder auditRecorder,
        TimeProvider timeProvider,
        ILogger<BffCallbackHandler> logger
    )
    {
        _frontends = frontends;
        _transactionService = transactionService;
        _cookies = cookies;
        _metadataResolver = metadataResolver;
        _tokenClient = tokenClient;
        _idTokenValidator = idTokenValidator;
        _userInfoClient = userInfoClient;
        _gates = gates;
        _claimContributors = claimContributors;
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

        var frontend = _frontends.GetRequired(httpContext);

        httpContext.Response.SetNoStore();
        _cookies.DeleteLogin(httpContext.Response, frontend);

        var stateValues = request.Query[OpenIdProtocol.Parameters.State];
        var state = stateValues.Count == 1 ? stateValues[0] : null;
        var loginSecret = _cookies.ReadLoginSecret(request, frontend);
        var taken = await _transactionService.TakeAsync(state, loginSecret, cancellationToken);
        if (taken.Transaction is not { } transaction)
        {
            await FailAsync(
                httpContext,
                frontend,
                taken.FailureReason ?? BffLoginFailureReasons.UnknownState
            );
            return;
        }

        if (!string.Equals(transaction.Frontend, frontend.Name, StringComparison.Ordinal))
        {
            await FailAsync(httpContext, frontend, BffLoginFailureReasons.FrontendMismatch);
            return;
        }

        if (request.Query.ContainsKey(OpenIdProtocol.Parameters.ErrorCode))
        {
            await FailAsync(httpContext, frontend, BffLoginFailureReasons.AuthorizationError);
            return;
        }

        OpenIdProviderMetadata metadata;
        try
        {
            metadata = await _metadataResolver.GetMetadataAsync(
                transaction.Issuer,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (BffProviderFailures.IsUnavailable(exception, cancellationToken))
        {
            await FailAsync(httpContext, frontend, BffLoginFailureReasons.ProviderUnavailable);
            return;
        }

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
            await FailAsync(httpContext, frontend, BffLoginFailureReasons.IssuerMismatch);
            return;
        }

        var codeValues = request.Query[OpenIdProtocol.Parameters.AuthorizationCode];
        var code = codeValues.Count == 1 ? codeValues[0] : null;
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            await FailAsync(httpContext, frontend, BffLoginFailureReasons.InvalidResponse);
            return;
        }

        var registration = frontend.Registration;
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
        catch (Exception exception)
            when (BffProviderFailures.IsUnavailable(exception, cancellationToken))
        {
            await FailAsync(httpContext, frontend, BffLoginFailureReasons.ProviderUnavailable);
            return;
        }

        if (tokenResult.Response is not { IdToken: { } idToken, AccessToken: { } accessToken })
        {
            await FailAsync(httpContext, frontend, BffLoginFailureReasons.TokenError);
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
        IdTokenValidationResult validation;
        try
        {
            validation = await _idTokenValidator.ValidateAsync(
                registration,
                validationRequest,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (BffProviderFailures.IsUnavailable(exception, cancellationToken))
        {
            await FailAsync(httpContext, frontend, BffLoginFailureReasons.ProviderUnavailable);
            return;
        }

        if (validation.Identity is not { } identity || !validation.IsValid)
        {
            var reason = string.Equals(
                validation.FailureCode,
                TokenValidationFailureCodes.NonceMismatch,
                StringComparison.Ordinal
            )
                ? BffLoginFailureReasons.NonceMismatch
                : BffLoginFailureReasons.IdTokenInvalid;
            await FailAsync(httpContext, frontend, reason);
            return;
        }

        var subject = identity.FindFirst(JoseClaimNames.Payload.Sub)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            await FailAsync(httpContext, frontend, BffLoginFailureReasons.IdTokenInvalid);
            return;
        }

        var options = frontend.Options;
        IEnumerable<Claim> sessionClaims = BffSessionPrincipal.SelectSessionClaims(identity);
        if (options.UserInfo.Load)
        {
            // UserInfo is requested with the new access token, the way it was issued, and must be about the ID token's
            // subject (OpenID Connect Core 1.0 §5.3.2). See ADR-1299.
            var userInfoRequest = new UserInfoRequest(accessToken, subject)
            {
                DPoPJwkThumbprint = tokenResult.DPoPJwkThumbprint,
            };
            UserInfoResult userInfo;
            try
            {
                userInfo = await _userInfoClient.GetUserInfoAsync(
                    registration,
                    userInfoRequest,
                    cancellationToken
                );
            }
            catch (Exception exception)
                when (BffProviderFailures.IsUnavailable(exception, cancellationToken))
            {
                await FailAsync(httpContext, frontend, BffLoginFailureReasons.ProviderUnavailable);
                return;
            }

            if (!userInfo.IsSuccess)
            {
                await FailAsync(httpContext, frontend, BffLoginFailureReasons.UserInfoFailed);
                return;
            }

            sessionClaims = BffSessionPrincipal.MergeUserInfo(sessionClaims, userInfo.Identity);
        }

        // The contributors shape the session's claims once, before the gates, so a gate judges the session as it will
        // be created; none of them can change the claims that anchor it to the provider's authentication.
        var claimContext = new BffSessionClaimContext(
            httpContext,
            transaction.Issuer,
            subject,
            sessionClaims
        );
        foreach (var contributor in _claimContributors)
        {
            await contributor.ContributeAsync(claimContext, cancellationToken);
        }

        var claims = BffSessionPrincipal.StoreClaims(claimContext.Claims);
        var principal = BffSessionPrincipal.Create(claims, transaction.Issuer);
        var gateContext = new BffLoginGateContext(httpContext, transaction.Issuer, principal);
        foreach (var gate in _gates)
        {
            var allowed = await gate.AllowAsync(gateContext, cancellationToken);
            if (allowed)
                continue;

            await FailAsync(httpContext, frontend, BffLoginFailureReasons.RejectedByGate);
            return;
        }

        var response = tokenResult.Response;
        var now = _timeProvider.GetUtcNowWithPrecisionInSeconds();

        var absolute = now + options.Session.AbsoluteLifetime;
        if (response.RefreshTokenExpiresIn is { } refreshSeconds)
        {
            var refreshExpiresAt = now + TimeSpan.FromSeconds(refreshSeconds);
            if (refreshExpiresAt < absolute)
                absolute = refreshExpiresAt;
        }

        var idle = now + options.Session.IdleTimeout;
        if (absolute < idle)
            idle = absolute;

        var sessionMetadata = new BffSessionMetadata
        {
            Frontend = frontend.Name,
            Authority = frontend.Options.Authority,
            ClientId = frontend.Options.ClientId,
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

        var oldHandle = _cookies.ReadSessionHandle(request, frontend);
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
        _cookies.SetSession(httpContext.Response, frontend, created.Handle, absolute);
        await _auditRecorder.LoginSucceededAsync(httpContext, subject, cancellationToken);

        var returnUrl = BffNavigation.LocalUrl(request, transaction.ReturnUrl);
        httpContext.Response.Redirect(returnUrl);
    }

    private async ValueTask FailAsync(HttpContext httpContext, BffFrontend frontend, string reason)
    {
        _logger.LoginFailed(reason);
        await _auditRecorder.LoginFailedAsync(httpContext, reason, httpContext.RequestAborted);
        var errorUrl = BffNavigation.ErrorUrl(httpContext.Request, frontend.Options, reason);
        httpContext.Response.Redirect(errorUrl);
    }
}
