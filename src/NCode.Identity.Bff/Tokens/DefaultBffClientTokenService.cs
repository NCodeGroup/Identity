// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The default <see cref="IBffClientTokenService"/>, which keeps the BFF's own access tokens in memory on each replica
/// and requests one token at a time per resource and scope.
/// </summary>
// The token belongs to the BFF, not to a session, and the grant issues no refresh token: each replica requests its
// own, so no lease spans the replicas and nothing is persisted. The resource and scope come from the endpoint's
// configuration, never from the request, so the cache holds one entry per configured pair. See ADR-1381.
internal class DefaultBffClientTokenService(
    BffGrantClient grantClient,
    IOptions<BffOptions> options,
    TimeProvider timeProvider
) : IBffClientTokenService
{
    private readonly BffGrantClient _grantClient = grantClient;
    private readonly IOptions<BffOptions> _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    private BffRefreshLocks Locks { get; } = new();

    private ConcurrentDictionary<string, BffAccessToken> Tokens { get; } =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        string? resource,
        string? scope,
        CancellationToken cancellationToken
    )
    {
        var key = $"{resource}\n{scope}";
        var fresh = TryGetFresh(key);
        if (fresh is not null)
            return fresh;

        using var held = await Locks.TryEnterAsync(
            key,
            _options.Value.Tokens.RefreshLeaseDuration,
            cancellationToken
        );
        if (held is null)
            return BffTokenResult.Failure(503, BffTokenFailureReasons.RefreshWaitTimeout);

        // another request may have requested the token while this one waited
        fresh = TryGetFresh(key);
        if (fresh is not null)
            return fresh;

        var request = new ClientCredentialsTokenRequest
        {
            Resources = resource is null ? [] : [resource],
            Scope = scope,
        };
        var result = await _grantClient.RequestAsync(request, cancellationToken);
        if (result.Token is { } token)
        {
            Tokens[key] = token;
        }

        return result;
    }

    private BffTokenResult? TryGetFresh(string key)
    {
        if (!Tokens.TryGetValue(key, out var token))
            return null;

        var now = _timeProvider.GetUtcNow();
        var refreshBeforeExpiry = _options.Value.Tokens.RefreshBeforeExpiry;
        return token.ExpiresAt - refreshBeforeExpiry > now ? BffTokenResult.Success(token) : null;
    }
}
