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

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Diagnostics;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Time;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The default <see cref="IBffUserTokenService"/>, which serves cached tokens and refreshes stale ones.
/// </summary>
internal class DefaultBffUserTokenService(
    BffSessionResolver sessionResolver,
    IBffSessionManager sessionManager,
    BffInstrumentedSessionStore sessionStore,
    ITokenClient tokenClient,
    BffClientRegistrationProvider registrationProvider,
    BffRefreshLocks refreshLocks,
    BffMetrics metrics,
    BffCookies cookies,
    IBffAuditRecorder auditRecorder,
    IOptions<BffOptions> options,
    TimeProvider timeProvider,
    ILogger<DefaultBffUserTokenService> logger
) : IBffUserTokenService
{
    private readonly BffSessionResolver _sessionResolver = sessionResolver;
    private readonly IBffSessionManager _sessionManager = sessionManager;
    private readonly BffInstrumentedSessionStore _sessionStore = sessionStore;
    private readonly ITokenClient _tokenClient = tokenClient;
    private readonly BffClientRegistrationProvider _registrationProvider = registrationProvider;
    private readonly BffRefreshLocks _refreshLocks = refreshLocks;
    private readonly BffMetrics _metrics = metrics;
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
        using var activity = BffActivities.Source.StartActivity(BffActivities.TokenRefresh);
        var startTimestamp = Stopwatch.GetTimestamp();
        var attempt = new RefreshAttempt();
        try
        {
            var result = await RefreshGatedAsync(
                httpContext,
                state,
                resource,
                scope,
                attempt,
                cancellationToken
            );
            RefreshCompleted(activity, startTimestamp, attempt, result.Reason);
            return result;
        }
        catch (Exception exception)
        {
            RefreshCompleted(activity, startTimestamp, attempt, exception.GetType().FullName);
            throw;
        }
    }

    // The refresh is measured as the request experienced it: refreshed when it presented the refresh token, joined when
    // another request or replica refreshed the session for it, failed otherwise. See ADR-0197 and ADR-0298.
    private void RefreshCompleted(
        Activity? activity,
        long startTimestamp,
        RefreshAttempt attempt,
        string? failure
    )
    {
        var result =
            failure is not null ? BffMetrics.FailedResult
            : attempt.Presented ? BffMetrics.RefreshedResult
            : BffMetrics.JoinedResult;
        _metrics.TokenRefreshCompleted(startTimestamp, result, failure);

        if (activity is null)
            return;

        activity.SetTag(BffMetrics.RefreshResultAttribute, result);
        if (failure is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error);
        activity.SetTag(BffMetrics.ErrorTypeAttribute, failure);
    }

    private async ValueTask<BffTokenResult> RefreshGatedAsync(
        HttpContext httpContext,
        BffSessionState state,
        string? resource,
        string? scope,
        RefreshAttempt attempt,
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
            return await RefreshLockedAsync(
                httpContext,
                state,
                resource,
                scope,
                attempt,
                cancellationToken
            );
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
        RefreshAttempt attempt,
        CancellationToken cancellationToken
    )
    {
        var current = await _sessionManager.LoadAsync(state.Handle, cancellationToken);
        if (current is null)
            return BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired);

        var result = await AcquireAndRefreshAsync(
            httpContext,
            current,
            resource,
            scope,
            attempt,
            cancellationToken
        );
        if (result is not null)
            return result;

        return await WaitForRefreshAsync(
            httpContext,
            current,
            resource,
            scope,
            attempt,
            cancellationToken
        );
    }

    private BffTokenResult? TryGetFresh(
        BffSessionPayload payload,
        string? resource,
        string? scope,
        DateTimeOffset now
    )
    {
        if (
            !BffTokenCache.TryGetFresh(
                payload,
                BffTokenRequirementNames.User,
                resource,
                scope,
                now,
                _options.Value.Tokens.RefreshBeforeExpiry,
                out var cached
            )
        )
            return null;

        var token = new BffAccessToken(cached.Value, cached.TokenType, cached.ExpiresAt)
        {
            DPoPJwkThumbprint = cached.DPoPJwkThumbprint,
        };
        return BffTokenResult.Success(token);
    }

    private static string GetLeaseKey(BffSessionState session, string refreshToken)
    {
        // the lease guards the invariant itself: a refresh token is never presented twice
        var fingerprint = BffKeyDerivation.Fingerprint(refreshToken);
        return $"{session.StoreKey}:{fingerprint}";
    }

    /// <summary>
    /// Serves a fresh cached token or tries to win the lease and refresh; returns <c>null</c> when
    /// another replica holds the lease.
    /// </summary>
    private async ValueTask<BffTokenResult?> AcquireAndRefreshAsync(
        HttpContext httpContext,
        BffSessionState current,
        string? resource,
        string? scope,
        RefreshAttempt attempt,
        CancellationToken cancellationToken
    )
    {
        var tokenOptions = _options.Value.Tokens;

        var now = _timeProvider.GetUtcNowWithPrecisionInSeconds();
        var fresh = TryGetFresh(current.Payload, resource, scope, now);
        if (fresh is not null)
            return fresh;

        var refreshToken = current.Payload.RefreshToken;
        if (refreshToken is null)
            return await EndRefreshFailedAsync(httpContext, current);

        var leaseKey = GetLeaseKey(current, refreshToken);
        var acquiredAt = now;
        var leaseExpiresAt = acquiredAt + tokenOptions.RefreshLeaseDuration;
        var acquired = await _sessionStore.TryAcquireRefreshLeaseAsync(
            leaseKey,
            leaseExpiresAt,
            cancellationToken
        );
        if (!acquired)
            return null;

        attempt.Presented = true;
        try
        {
            return await RequestAndStoreAsync(
                httpContext,
                current,
                refreshToken,
                resource,
                scope,
                now
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

    private async ValueTask<BffTokenResult> WaitForRefreshAsync(
        HttpContext httpContext,
        BffSessionState seen,
        string? resource,
        string? scope,
        RefreshAttempt attempt,
        CancellationToken cancellationToken
    )
    {
        // the old refresh token is never presented again: wait for the winner's write
        var seenToken = seen.Payload.RefreshToken ?? string.Empty;
        var leaseKey = GetLeaseKey(seen, seenToken);
        var deadline = _timeProvider.GetUtcNow() + _options.Value.Tokens.RefreshLeaseDuration;
        var delay = TimeSpan.FromMilliseconds(50);
        var maxDelay = TimeSpan.FromMilliseconds(800);

        while (true)
        {
            await Task.Delay(delay, _timeProvider, cancellationToken);
            delay = delay * 2 < maxDelay ? delay * 2 : maxDelay;

            var loaded = await _sessionManager.LoadAsync(seen.Handle, cancellationToken);
            if (loaded is null)
                return BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired);

            var now = _timeProvider.GetUtcNowWithPrecisionInSeconds();
            var fresh = TryGetFresh(loaded.Payload, resource, scope, now);
            if (fresh is not null)
                return fresh;

            var loadedToken = loaded.Payload.RefreshToken;
            var changed = !string.Equals(loadedToken, seenToken, StringComparison.Ordinal);
            if (
                changed || !await _sessionStore.IsRefreshLeaseHeldAsync(leaseKey, cancellationToken)
            )
            {
                var result = await AcquireAndRefreshAsync(
                    httpContext,
                    loaded,
                    resource,
                    scope,
                    attempt,
                    cancellationToken
                );
                if (result is not null)
                    return result;

                seen = loaded;
                seenToken = loadedToken ?? string.Empty;
                leaseKey = GetLeaseKey(seen, seenToken);
            }

            if (_timeProvider.GetUtcNow() >= deadline)
                return BffTokenResult.Failure(503, BffTokenFailureReasons.RefreshWaitTimeout);
        }
    }

    private async ValueTask<BffTokenResult> RequestAndStoreAsync(
        HttpContext httpContext,
        BffSessionState current,
        string refreshToken,
        string? resource,
        string? scope,
        DateTimeOffset now
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
            // The lease is won: a caller that aborts now must not lose the rotated refresh token, so nothing
            // but the client's request timeout ends the request or the write-back. See ADR-0291.
            result = await _tokenClient.RequestTokenAsync(
                _registrationProvider.Registration,
                request,
                CancellationToken.None
            );
        }
        catch (Exception exception)
            when (BffProviderFailures.IsUnavailable(exception, CancellationToken.None))
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
        if (response.RefreshTokenExpiresIn is { } refreshSeconds)
        {
            refreshExpiresAt = now + TimeSpan.FromSeconds(refreshSeconds);
        }

        // a lost write is merged onto the newer version and never dropped
        var updated = await _sessionManager.UpdateAsync(
            current,
            s => ApplyRefresh(s, entry, refreshToken, response.RefreshToken, refreshExpiresAt),
            CancellationToken.None
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

    // Whether this request presented the refresh token, so a refresh it got without presenting it counts as joined.
    private sealed class RefreshAttempt
    {
        public bool Presented { get; set; }
    }
}
