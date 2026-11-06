// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The default <see cref="IBffClientTokenService"/>, which keeps the BFF's own access tokens in memory on each replica
/// and requests one token at a time per frontend, resource and scope.
/// </summary>
// The token belongs to the BFF, not to a session, and the grant issues no refresh token: each replica requests its
// own, so no lease spans the replicas and nothing is persisted. The resource and scope come from the endpoint's
// configuration, never from the request, so the cache holds one entry per frontend and configured resource/scope
// pair. See ADR-1381.
internal class DefaultBffClientTokenService(
    BffGrantClient grantClient,
    BffRefreshLocks locks,
    BffFrontendCatalog frontends,
    IOptions<BffOptions> options,
    TimeProvider timeProvider
) : IBffClientTokenService
{
    private readonly BffGrantClient _grantClient = grantClient;
    private readonly BffRefreshLocks _locks = locks;
    private readonly BffFrontendCatalog _frontends = frontends;
    private readonly IOptions<BffOptions> _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    private ConcurrentDictionary<
        (string Frontend, string? Resource, string? Scope),
        BffAccessToken
    > Tokens { get; } = [];

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(request);

        var frontend = _frontends.Resolve(httpContext);
        if (frontend is null)
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            );

        var key = (frontend.Name, request.Resource, request.Scope);
        var fresh = TryGetFresh(key);
        if (fresh is not null)
            return fresh;

        using var held = await _locks.TryEnterAsync(
            BffRefreshLocks.ClientPurpose,
            $"{frontend.Name}\n{request.Resource}\n{request.Scope}",
            _options.Value.Tokens.RefreshLeaseDuration,
            cancellationToken
        );
        if (held is null)
            return BffTokenResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                BffTokenFailureReasons.RefreshWaitTimeout
            );

        // another request may have requested the token while this one waited
        fresh = TryGetFresh(key);
        if (fresh is not null)
            return fresh;

        var grantRequest = new ClientCredentialsTokenRequest
        {
            Resources = request.Resource is null ? [] : [request.Resource],
            Scope = request.Scope,
        };
        var result = await _grantClient.RequestAsync(frontend, grantRequest, cancellationToken);
        if (result.Token is { } token)
        {
            Tokens[key] = token;
        }

        return result;
    }

    private BffTokenResult? TryGetFresh((string Frontend, string? Resource, string? Scope) key)
    {
        if (!Tokens.TryGetValue(key, out var token))
            return null;

        var now = _timeProvider.GetUtcNow();
        var refreshBeforeExpiry = _options.Value.Tokens.RefreshBeforeExpiry;
        return token.ExpiresAt - refreshBeforeExpiry > now ? BffTokenResult.Success(token) : null;
    }
}
