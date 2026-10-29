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
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Json;
using NCode.Time;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The default <see cref="IBffUserTokenService"/>, which serves cached tokens and refreshes stale ones.
/// </summary>
internal class DefaultBffUserTokenService(
    BffSessionResolver sessionResolver,
    IBffSessionManager sessionManager,
    IBffSessionStore sessionStore,
    ITokenClient tokenClient,
    BffClientRegistrationProvider registrationProvider,
    BffRefreshLocks refreshLocks,
    BffCookies cookies,
    IBffAuditRecorder auditRecorder,
    IOptions<BffOptions> options,
    TimeProvider timeProvider,
    ILogger<DefaultBffUserTokenService> logger
) : IBffUserTokenService
{
    private const string RefreshTokenExpiresInMember = "refresh_token_expires_in";

    private readonly BffSessionResolver _sessionResolver = sessionResolver;
    private readonly IBffSessionManager _sessionManager = sessionManager;
    private readonly IBffSessionStore _sessionStore = sessionStore;
    private readonly ITokenClient _tokenClient = tokenClient;
    private readonly BffClientRegistrationProvider _registrationProvider = registrationProvider;
    private readonly BffRefreshLocks _refreshLocks = refreshLocks;
    private readonly BffCookies _cookies = cookies;
    private readonly IBffAuditRecorder _auditRecorder = auditRecorder;
    private readonly IOptions<BffOptions> _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<DefaultBffUserTokenService> _logger = logger;

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        string? resource,
        string? scope,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var state = await _sessionResolver.ResolveAsync(httpContext, cancellationToken);
        if (state is null)
            return BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired);

        var now = _timeProvider.GetUtcNow();
        var refreshBeforeExpiry = _options.Value.Tokens.RefreshBeforeExpiry;
        if (
            BffTokenCache.TryGetFresh(
                state.Payload,
                BffTokenRequirementNames.User,
                resource,
                scope,
                now,
                refreshBeforeExpiry,
                out var cached
            )
        )
        {
            var token = new BffAccessToken(cached.Value, cached.TokenType, cached.ExpiresAt)
            {
                DPoPJwkThumbprint = cached.DPoPJwkThumbprint,
            };
            return BffTokenResult.Success(token);
        }

        if (state.Payload.RefreshToken is null)
            return await EndRefreshFailedAsync(httpContext, state);

        return await RefreshAsync(httpContext, state, resource, scope, cancellationToken);
    }

    private async ValueTask<BffTokenResult> EndRefreshFailedAsync(
        HttpContext httpContext,
        BffSessionState state
    )
    {
        await _sessionManager.EndAsync(
            httpContext,
            state.StoreKey,
            state.Metadata,
            BffSessionEndReasons.RefreshFailed,
            CancellationToken.None
        );
        _cookies.DeleteSession(httpContext.Response);
        return BffTokenResult.Failure(401, BffTokenFailureReasons.RefreshFailed);
    }

    private async ValueTask<BffTokenResult> RefreshAsync(
        HttpContext httpContext,
        BffSessionState state,
        string? resource,
        string? scope,
        CancellationToken cancellationToken
    )
    {
        var tokenOptions = _options.Value.Tokens;

        // inside a replica, refreshes of one session are serialized by a striped lock
        var gate = _refreshLocks.For(state.StoreKey);
        var entered = await gate.WaitAsync(tokenOptions.RefreshLeaseDuration, cancellationToken);
        if (!entered)
            return BffTokenResult.Failure(503, BffTokenFailureReasons.RefreshWaitTimeout);

        try
        {
            return await RefreshLockedAsync(httpContext, state, resource, scope, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<BffTokenResult> RefreshLockedAsync(
        HttpContext httpContext,
        BffSessionState state,
        string? resource,
        string? scope,
        CancellationToken cancellationToken
    )
    {
        var tokenOptions = _options.Value.Tokens;

        var current = await _sessionManager.LoadAsync(state.Handle, cancellationToken);
        if (current is null)
            return BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired);

        var now = _timeProvider.GetUtcNowWithPrecisionInSeconds();
        if (
            BffTokenCache.TryGetFresh(
                current.Payload,
                BffTokenRequirementNames.User,
                resource,
                scope,
                now,
                tokenOptions.RefreshBeforeExpiry,
                out var cached
            )
        )
        {
            var cachedToken = new BffAccessToken(cached.Value, cached.TokenType, cached.ExpiresAt)
            {
                DPoPJwkThumbprint = cached.DPoPJwkThumbprint,
            };
            return BffTokenResult.Success(cachedToken);
        }

        var refreshToken = current.Payload.RefreshToken;
        if (refreshToken is null)
            return await EndRefreshFailedAsync(httpContext, current);

        // the lease guards the invariant itself: a refresh token is never presented twice
        var fingerprint = BffKeyDerivation.Fingerprint(refreshToken);
        var leaseKey = $"{current.StoreKey}:{fingerprint}";
        var acquiredAt = now;
        var leaseExpiresAt = acquiredAt + tokenOptions.RefreshLeaseDuration;
        var acquired = await _sessionStore.TryAcquireRefreshLeaseAsync(
            leaseKey,
            leaseExpiresAt,
            cancellationToken
        );
        if (!acquired)
        {
            // step 49 waits for the winner of the lease to write the refreshed tokens
            return BffTokenResult.Failure(503, BffTokenFailureReasons.RefreshWaitTimeout);
        }

        try
        {
            return await RequestAndStoreAsync(
                httpContext,
                current,
                refreshToken,
                resource,
                scope,
                now,
                cancellationToken
            );
        }
        finally
        {
            // never remove a lease that has expired and that another replica may have taken
            var finishedAt = _timeProvider.GetUtcNowWithPrecisionInSeconds();
            var safetyMargin = TimeSpan.FromSeconds(5);
            if (finishedAt - acquiredAt < tokenOptions.RefreshLeaseDuration - safetyMargin)
                await _sessionStore.ReleaseRefreshLeaseAsync(leaseKey, CancellationToken.None);
        }
    }

    private async ValueTask<BffTokenResult> RequestAndStoreAsync(
        HttpContext httpContext,
        BffSessionState current,
        string refreshToken,
        string? resource,
        string? scope,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var request = new RefreshTokenRequest(refreshToken)
        {
            Resources = resource is null ? [] : [resource],
            Scope = scope,
            DPoPJwkThumbprint = current.Payload.DPoPJwkThumbprint,
        };
        var subject = current.Metadata.Subject;

        TokenResult result;
        try
        {
            result = await _tokenClient.RequestTokenAsync(
                _registrationProvider.Registration,
                request,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (exception is OpenIdProviderException or HttpRequestException
                || (
                    exception is OperationCanceledException
                    && !cancellationToken.IsCancellationRequested
                )
            )
        {
            _logger.RefreshFailed(BffTokenFailureReasons.ProviderUnavailable, exception);
            await _auditRecorder.TokenRefreshFailedAsync(
                httpContext,
                subject,
                BffTokenFailureReasons.ProviderUnavailable,
                CancellationToken.None
            );
            return BffTokenResult.Failure(503, BffTokenFailureReasons.ProviderUnavailable);
        }

        if (!result.IsSuccess)
        {
            var error = result.Error.Error;
            _logger.RefreshFailed(error, null);
            await _auditRecorder.TokenRefreshFailedAsync(
                httpContext,
                subject,
                error,
                CancellationToken.None
            );

            if (
                !string.Equals(
                    error,
                    OpenIdProtocol.ErrorCodes.InvalidGrant,
                    StringComparison.Ordinal
                )
            )
                return BffTokenResult.Failure(502, BffTokenFailureReasons.ProviderError);

            await _sessionManager.EndAsync(
                httpContext,
                current.StoreKey,
                current.Metadata,
                BffSessionEndReasons.RefreshFailed,
                CancellationToken.None
            );
            _cookies.DeleteSession(httpContext.Response);
            return BffTokenResult.Failure(401, BffTokenFailureReasons.RefreshFailed);
        }

        var response = result.Response;
        var accessToken = response.AccessToken;
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.RefreshFailed(BffTokenFailureReasons.ProviderError, null);
            await _auditRecorder.TokenRefreshFailedAsync(
                httpContext,
                subject,
                BffTokenFailureReasons.ProviderError,
                CancellationToken.None
            );
            return BffTokenResult.Failure(502, BffTokenFailureReasons.ProviderError);
        }

        var expiresAt = response.ExpiresIn is { } expiresIn
            ? now + TimeSpan.FromSeconds(expiresIn)
            : now;
        var entry = new BffStoredAccessToken
        {
            Requirement = BffTokenRequirementNames.User,
            Resource = resource,
            Scope = scope,
            Value = accessToken,
            TokenType = response.TokenType ?? string.Empty,
            ExpiresAt = expiresAt,
            DPoPJwkThumbprint = result.DPoPJwkThumbprint,
        };

        DateTimeOffset? refreshExpiresAt = null;
        if (
            response.ExtensionData is { } extensionData
            && extensionData.TryGetValue(RefreshTokenExpiresInMember, out var refreshExpiry)
            && refreshExpiry.TryGetWholeInt32(out var refreshSeconds)
            && refreshSeconds > 0
        )
        {
            refreshExpiresAt = now + TimeSpan.FromSeconds(refreshSeconds);
        }

        // a lost write is merged onto the newer version and never dropped
        var updated = await _sessionManager.UpdateAsync(
            current,
            s => ApplyRefresh(s, entry, refreshToken, response.RefreshToken, refreshExpiresAt),
            cancellationToken
        );
        if (updated is null)
            return BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired);

        var token = new BffAccessToken(entry.Value, entry.TokenType, entry.ExpiresAt)
        {
            DPoPJwkThumbprint = entry.DPoPJwkThumbprint,
        };
        return BffTokenResult.Success(token);
    }

    private static BffSessionState ApplyRefresh(
        BffSessionState session,
        BffStoredAccessToken entry,
        string presentedRefreshToken,
        string? issuedRefreshToken,
        DateTimeOffset? refreshExpiresAt
    )
    {
        var payload = BffTokenCache.WithToken(session.Payload, entry);
        if (
            string.Equals(
                session.Payload.RefreshToken,
                presentedRefreshToken,
                StringComparison.Ordinal
            )
        )
        {
            payload = payload with { RefreshToken = issuedRefreshToken ?? presentedRefreshToken };
        }

        var metadata = session.Metadata;
        if (refreshExpiresAt is not { } cap || cap >= metadata.AbsoluteExpiresAt)
            return session with { Payload = payload };

        var idleExpiresAt = metadata.IdleExpiresAt < cap ? metadata.IdleExpiresAt : cap;
        var capped = new BffSessionMetadata
        {
            Frontend = metadata.Frontend,
            Issuer = metadata.Issuer,
            Subject = metadata.Subject,
            SessionId = metadata.SessionId,
            CreatedAt = metadata.CreatedAt,
            LastAccessedAt = metadata.LastAccessedAt,
            AbsoluteExpiresAt = cap,
            IdleExpiresAt = idleExpiresAt,
            ExtensionData = metadata.ExtensionData,
        };
        return session with { Metadata = capped, Payload = payload };
    }
}
