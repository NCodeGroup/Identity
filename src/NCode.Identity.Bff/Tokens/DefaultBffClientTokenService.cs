// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The default <see cref="IBffClientTokenService"/>, which keeps the BFF's own access tokens in memory on each replica
/// and requests one token at a time per frontend, resource and scope.
/// </summary>
// The token belongs to the BFF, not to a session, and the grant issues no refresh token: each replica requests its
// own, so no lease spans the replicas and nothing is persisted. The resource and scope come from the endpoint's
// configuration, never from the request, so the cache holds one entry per frontend instance and configured
// resource/scope pair. A frontend that a configuration change rebuilds never reuses a token its previous
// registration obtained. See ADR-1381, ADR-1442.
internal class DefaultBffClientTokenService(
    BffGrantClient grantClient,
    BffRefreshLocks locks,
    BffFrontendCatalog frontends,
    TimeProvider timeProvider
) : IBffClientTokenService
{
    private readonly BffGrantClient _grantClient = grantClient;
    private readonly BffRefreshLocks _locks = locks;
    private readonly BffFrontendCatalog _frontends = frontends;
    private readonly TimeProvider _timeProvider = timeProvider;

    private ConditionalWeakTable<
        BffFrontend,
        ConcurrentDictionary<(string? Resource, string? Scope), BffAccessToken>
    > Tokens { get; } = new();

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffSessionScope scope,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);

        var frontend = scope.SelectedFrontend;
        if (frontend is null)
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            );

        var tokens = Tokens.GetOrCreateValue(frontend);
        var key = (request.Resource, request.Scope);
        var fresh = TryGetFresh(tokens, key);
        if (fresh is not null)
            return fresh;

        using var held = await _locks.TryEnterAsync(
            BffRefreshLocks.ClientPurpose,
            $"{frontend.Name}\n{request.Resource}\n{request.Scope}",
            _frontends.Current.Options.Tokens.RefreshLeaseDuration,
            cancellationToken
        );
        if (held is null)
            return BffTokenResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                BffTokenFailureReasons.RefreshWaitTimeout
            );

        // another request may have requested the token while this one waited
        fresh = TryGetFresh(tokens, key);
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
            tokens[key] = token;
        }

        return result;
    }

    private BffTokenResult? TryGetFresh(
        ConcurrentDictionary<(string? Resource, string? Scope), BffAccessToken> tokens,
        (string? Resource, string? Scope) key
    )
    {
        if (!tokens.TryGetValue(key, out var token))
            return null;

        var now = _timeProvider.GetUtcNow();
        var refreshBeforeExpiry = _frontends.Current.Options.Tokens.RefreshBeforeExpiry;
        return token.ExpiresAt - refreshBeforeExpiry > now ? BffTokenResult.Success(token) : null;
    }
}
