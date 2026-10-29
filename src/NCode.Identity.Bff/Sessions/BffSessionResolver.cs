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
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Resolves the BFF session of a request from its cookie, and ends the session when it has expired.
/// </summary>
internal class BffSessionResolver(
    IBffSessionManager sessionManager,
    BffCookies cookies,
    IEnumerable<IBffSessionValidator> validators,
    IOptions<BffOptions> options,
    TimeProvider timeProvider
)
{
    private readonly IBffSessionManager _sessionManager = sessionManager;
    private readonly BffCookies _cookies = cookies;
    private readonly IEnumerable<IBffSessionValidator> _validators = validators;
    private readonly IOptions<BffOptions> _options = options;
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

        var handle = _cookies.ReadSessionHandle(httpContext.Request);
        if (handle is null)
            return Complete(httpContext, feature, null);

        var state = await _sessionManager.LoadAsync(handle, cancellationToken);
        if (state is null)
        {
            _cookies.DeleteSession(httpContext.Response);
            return Complete(httpContext, feature, null);
        }

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
            _cookies.DeleteSession(httpContext.Response);
            return Complete(httpContext, feature, null);
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
            _cookies.DeleteSession(httpContext.Response);
            return Complete(httpContext, feature, null);
        }

        var slides = BffGuardedEndpoints.IsApi(endpoint) || BffGuardedEndpoints.IsPage(endpoint);
        var idleTimeout = _options.Value.Session.IdleTimeout;
        if (slides && now - state.Metadata.LastAccessedAt >= idleTimeout / 10)
        {
            var slid = await _sessionManager.UpdateAsync(
                state,
                current => Slide(current, now, idleTimeout),
                cancellationToken
            );
            if (slid is null)
            {
                _cookies.DeleteSession(httpContext.Response);
                return Complete(httpContext, feature, null);
            }

            state = slid;
        }

        return Complete(httpContext, feature, state);
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

    private static BffSessionState? Complete(
        HttpContext httpContext,
        BffRequestFeature feature,
        BffSessionState? state
    )
    {
        feature.IsResolved = true;
        feature.Session = state;
        if (state is null)
            return null;

        var principal = BffSessionPrincipal.Create(state.Payload.Claims, state.Metadata.Issuer);
        feature.Principal = principal;
        httpContext.Features.Set<IBffSessionFeature>(
            new BffSessionFeature(state.Metadata, principal)
        );
        return state;
    }
}
