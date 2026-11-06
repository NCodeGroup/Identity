// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Navigation;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Resolves the BFF session of a request from its cookie, and ends the session when it has expired.
/// </summary>
internal class BffSessionResolver(
    IBffSessionManager sessionManager,
    BffCookies cookies,
    IEnumerable<IBffSessionValidator> validators,
    BffFrontendCatalog frontends,
    TimeProvider timeProvider
)
{
    private readonly IBffSessionManager _sessionManager = sessionManager;
    private readonly BffCookies _cookies = cookies;
    private readonly IEnumerable<IBffSessionValidator> _validators = validators;
    private readonly BffFrontendCatalog _frontends = frontends;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Resolves the session of the request, or returns <see langword="null"/> when the request has none.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    public virtual async ValueTask<BffSessionState?> ResolveAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var feature =
            httpContext.Features.Get<BffRequestFeature>()
            ?? throw new InvalidOperationException(
                "UseBff() must be called before UseAuthentication() and the BFF endpoints."
            );

        var endpoint = httpContext.GetEndpoint();
        var isProtected = BffGuardedEndpoints.IsGuarded(endpoint);
        if (isProtected && !ReferenceEquals(feature.CheckedEndpoint, endpoint))
            throw new InvalidOperationException("UseBff() must run after routing.");

        if (feature.IsResolved)
            return feature.Session;

        var frontend = _frontends.Resolve(httpContext);
        if (frontend is null)
            return CompleteWithoutSession(feature);

        var handle = _cookies.ReadSessionHandle(httpContext.Request, frontend);
        if (handle is null)
            return CompleteWithoutSession(feature);

        var slides =
            BffGuardedEndpoints.IsApi(endpoint)
            || BffGuardedEndpoints.IsHub(endpoint)
            || BffGuardedEndpoints.IsPage(endpoint);
        DateTimeOffset? activityAt = slides ? _timeProvider.GetUtcNow() : null;

        var state = await ValidateAsync(
            frontend,
            handle,
            httpContext,
            activityAt,
            cancellationToken
        );
        if (state is null)
        {
            _cookies.DeleteSession(httpContext.Response, frontend);
            return CompleteWithoutSession(feature);
        }

        return Complete(httpContext, feature, frontend, state);
    }

    /// <summary>
    /// Loads and validates the session of a handle, ends it when it has expired or a validator refuses it, and slides
    /// it from <paramref name="activityAt"/>. It never touches cookies or request features.
    /// </summary>
    /// <param name="frontend">The frontend that the session must belong to.</param>
    /// <param name="handle">The session handle.</param>
    /// <param name="httpContext">The request that resolved the session, or <see langword="null"/> when a connection revalidates it.</param>
    /// <param name="activityAt">The time of the activity that slides the session, or <see langword="null"/> to not slide it.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The valid session, or <see langword="null"/> when there is none.</returns>
    public virtual async ValueTask<BffSessionState?> ValidateAsync(
        BffFrontend frontend,
        string handle,
        HttpContext? httpContext,
        DateTimeOffset? activityAt,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(frontend);
        ArgumentException.ThrowIfNullOrEmpty(handle);

        var state = await _sessionManager.LoadAsync(handle, cancellationToken);
        if (state is null)
            return null;

        if (!string.Equals(state.Metadata.Frontend, frontend.Name, StringComparison.Ordinal))
            return null;

        var now = _timeProvider.GetUtcNow();
        string? endReason = null;
        if (now >= state.Metadata.AbsoluteExpiresAt)
            endReason = BffSessionEndReasons.AbsoluteExpiry;
        else if (now >= state.Metadata.IdleExpiresAt)
            endReason = BffSessionEndReasons.IdleTimeout;

        if (endReason is not null)
        {
            await _sessionManager.EndAsync(
                httpContext,
                state.StoreKey,
                state.Metadata,
                endReason,
                cancellationToken
            );
            return null;
        }

        var principal = BffSessionPrincipal.Create(state.Payload.Claims, state.Metadata.Issuer);
        var validationContext = new BffSessionValidationContext(
            httpContext,
            state.Metadata,
            principal
        );
        foreach (var validator in _validators)
        {
            var isValid = await validator.ValidateAsync(validationContext, cancellationToken);
            if (isValid)
                continue;

            await _sessionManager.EndAsync(
                httpContext,
                state.StoreKey,
                state.Metadata,
                BffSessionEndReasons.EndedByValidator,
                cancellationToken
            );
            return null;
        }

        var idleTimeout = frontend.Options.Session.IdleTimeout;
        if (activityAt is { } at && at - state.Metadata.LastAccessedAt >= idleTimeout / 10)
        {
            return await _sessionManager.UpdateAsync(
                state,
                current => Slide(current, at, idleTimeout),
                cancellationToken
            );
        }

        return state;
    }

    private static BffSessionState Slide(
        BffSessionState current,
        DateTimeOffset now,
        TimeSpan idleTimeout
    )
    {
        var metadata = current.Metadata;
        var idleExpiresAt = now + idleTimeout;
        if (idleExpiresAt > metadata.AbsoluteExpiresAt)
            idleExpiresAt = metadata.AbsoluteExpiresAt;

        var slid = new BffSessionMetadata
        {
            Frontend = metadata.Frontend,
            Issuer = metadata.Issuer,
            Subject = metadata.Subject,
            SessionId = metadata.SessionId,
            CreatedAt = metadata.CreatedAt,
            LastAccessedAt = now,
            AbsoluteExpiresAt = metadata.AbsoluteExpiresAt,
            IdleExpiresAt = idleExpiresAt,
            ExtensionData = metadata.ExtensionData,
        };
        return current with { Metadata = slid };
    }

    private static BffSessionState? CompleteWithoutSession(BffRequestFeature feature)
    {
        feature.IsResolved = true;
        feature.Session = null;
        return null;
    }

    private static BffSessionState Complete(
        HttpContext httpContext,
        BffRequestFeature feature,
        BffFrontend frontend,
        BffSessionState state
    )
    {
        feature.IsResolved = true;
        feature.Session = state;

        var principal = BffSessionPrincipal.Create(
            state,
            BffNavigation.LogoutUrl(httpContext.Request, frontend.Options)
        );
        feature.Principal = principal;
        httpContext.Features.Set<IBffSessionFeature>(
            new BffSessionFeature(state.Metadata, principal)
        );
        return state;
    }
}
