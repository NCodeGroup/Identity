// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Diagnostics;
using NCode.Identity.Bff.Frontends;
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
    BffFrontendCatalog frontends,
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
    private readonly BffFrontendCatalog _frontends = frontends;
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
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(request);

        var state = await _sessionResolver.ResolveAsync(httpContext, cancellationToken);
        if (state is null)
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            );

        var now = _timeProvider.GetUtcNow();
        var refreshBeforeExpiry = _options.Value.Tokens.RefreshBeforeExpiry;
        if (
            BffTokenCache.TryGetFresh(
                state.Payload,
                BffTokenRequirementNames.User,
                request.Resource,
                request.Scope,
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

        return await RefreshAsync(httpContext, state, request, cancellationToken);
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
        return BffTokenResult.Failure(
            StatusCodes.Status401Unauthorized,
            BffTokenFailureReasons.RefreshFailed
        );
    }

    private async ValueTask<BffTokenResult> RefreshAsync(
        HttpContext httpContext,
        BffSessionState state,
        BffTokenRequest request,
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
                request,
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
    // another request or replica refreshed the session for it, failed otherwise. See ADR-1197 and ADR-1298.
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
        BffTokenRequest request,
        RefreshAttempt attempt,
        CancellationToken cancellationToken
    )
    {
        var tokenOptions = _options.Value.Tokens;

        // inside a replica, refreshes of one session wait on that session's own lock
        using var held = await _refreshLocks.TryEnterAsync(
            BffRefreshLocks.UserPurpose,
            state.StoreKey,
            tokenOptions.RefreshLeaseDuration,
            cancellationToken
        );
        if (held is null)
            return BffTokenResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                BffTokenFailureReasons.RefreshWaitTimeout
            );

        return await RefreshLockedAsync(httpContext, state, request, attempt, cancellationToken);
    }

    private async ValueTask<BffTokenResult> RefreshLockedAsync(
        HttpContext httpContext,
        BffSessionState state,
        BffTokenRequest request,
        RefreshAttempt attempt,
        CancellationToken cancellationToken
    )
    {
        var current = await _sessionManager.LoadAsync(state.Handle, cancellationToken);
        if (current is null)
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            );

        var result = await AcquireAndRefreshAsync(
            httpContext,
            current,
            request,
            attempt,
            cancellationToken
        );
        if (result is not null)
            return result;

        return await WaitForRefreshAsync(httpContext, current, request, attempt, cancellationToken);
    }

    private BffTokenResult? TryGetFresh(
        BffSessionPayload payload,
        BffTokenRequest request,
        DateTimeOffset now
    )
    {
        if (
            !BffTokenCache.TryGetFresh(
                payload,
                BffTokenRequirementNames.User,
                request.Resource,
                request.Scope,
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
        BffTokenRequest request,
        RefreshAttempt attempt,
        CancellationToken cancellationToken
    )
    {
        var tokenOptions = _options.Value.Tokens;

        var now = _timeProvider.GetUtcNowWithPrecisionInSeconds();
        var fresh = TryGetFresh(current.Payload, request, now);
        if (fresh is not null)
            return fresh;

        var refreshToken = current.Payload.RefreshToken;
        if (refreshToken is null)
            return await EndRefreshFailedAsync(httpContext, current);

        var leaseKey = GetLeaseKey(current, refreshToken);
        var leaseExpiresAt = now + tokenOptions.RefreshLeaseDuration;
        var acquired = await _sessionStore.TryAcquireRefreshLeaseAsync(
            leaseKey,
            leaseExpiresAt,
            cancellationToken
        );
        if (acquired is not { } lease)
            return null;

        attempt.Presented = true;
        try
        {
            return await RequestAndStoreAsync(httpContext, current, refreshToken, request, now);
        }
        finally
        {
            // releases only this hold: a lease another replica took after this one expired has another owner and stays
            await ReleaseLeaseAsync(lease);
        }
    }

    /// <summary>
    /// Releases the refresh lease that <paramref name="lease"/> holds, logging a session store that cannot be reached
    /// instead of failing. A lease that is not released expires on its own, so a store that cannot be reached never
    /// changes the refresh's result or hides its failure.
    /// </summary>
    /// <param name="lease">The refresh lease to release.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    // See ADR-1308.
    private async ValueTask ReleaseLeaseAsync(BffRefreshLease lease)
    {
        try
        {
            await _sessionStore.ReleaseRefreshLeaseAsync(lease, CancellationToken.None);
        }
        catch (BffStoreUnavailableException exception)
        {
            _logger.RefreshLeaseReleaseFailed(exception);
        }
    }

    private async ValueTask<BffTokenResult> WaitForRefreshAsync(
        HttpContext httpContext,
        BffSessionState seen,
        BffTokenRequest request,
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
                return BffTokenResult.Failure(
                    StatusCodes.Status401Unauthorized,
                    BffTokenFailureReasons.SessionRequired
                );

            var now = _timeProvider.GetUtcNowWithPrecisionInSeconds();
            var fresh = TryGetFresh(loaded.Payload, request, now);
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
                    request,
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
                return BffTokenResult.Failure(
                    StatusCodes.Status503ServiceUnavailable,
                    BffTokenFailureReasons.RefreshWaitTimeout
                );
        }
    }

    private async ValueTask<BffTokenResult> RequestAndStoreAsync(
        HttpContext httpContext,
        BffSessionState current,
        string refreshToken,
        BffTokenRequest request,
        DateTimeOffset now
    )
    {
        var refreshRequest = new RefreshTokenRequest(refreshToken)
        {
            Resources = request.Resource is null ? [] : [request.Resource],
            Scope = request.Scope,
            DPoPJwkThumbprint = current.Payload.DPoPJwkThumbprint,
        };
        var subject = current.Metadata.Subject;

        TokenResult result;
        try
        {
            // The lease is won: a caller that aborts now must not lose the rotated refresh token, so nothing
            // but the client's request timeout ends the request or the write-back. See ADR-1291.
            result = await _tokenClient.RequestTokenAsync(
                _frontends.GetRequired(httpContext).Registration,
                refreshRequest,
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
            return BffTokenResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                BffTokenFailureReasons.ProviderUnavailable
            );
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
                return BffTokenResult.Failure(
                    StatusCodes.Status502BadGateway,
                    BffTokenFailureReasons.ProviderError
                );

            await _sessionManager.EndAsync(
                httpContext,
                current.StoreKey,
                current.Metadata,
                BffSessionEndReasons.RefreshFailed,
                CancellationToken.None
            );
            _cookies.DeleteSession(httpContext.Response);
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.RefreshFailed
            );
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
            return BffTokenResult.Failure(
                StatusCodes.Status502BadGateway,
                BffTokenFailureReasons.ProviderError
            );
        }

        var expiresAt = response.ExpiresIn is { } expiresIn
            ? now + TimeSpan.FromSeconds(expiresIn)
            : now;
        var entry = new BffStoredAccessToken
        {
            Requirement = BffTokenRequirementNames.User,
            Resource = request.Resource,
            Scope = request.Scope,
            Value = accessToken,
            TokenType = string.IsNullOrEmpty(response.TokenType)
                ? OpenIdProtocol.TokenTypes.Bearer
                : response.TokenType,
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
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            );

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
