// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Looks up and updates the access tokens that a session caches.
/// </summary>
internal static class BffTokenCache
{
    /// <summary>
    /// The most access tokens that a session caches.
    /// </summary>
    public const int MaxEntries = 32;

    /// <summary>
    /// Finds a cached access token that is still fresh.
    /// </summary>
    /// <param name="payload">The session payload.</param>
    /// <param name="requirement">The token requirement name.</param>
    /// <param name="resource">The resource indicator, or <see langword="null"/>.</param>
    /// <param name="scope">The scope, or <see langword="null"/>.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="refreshBeforeExpiry">How long before expiry a token counts as stale.</param>
    /// <param name="token">The cached token, when one is fresh.</param>
    /// <returns><see langword="true"/> when a fresh token is cached.</returns>
    public static bool TryGetFresh(
        BffSessionPayload payload,
        string requirement,
        string? resource,
        string? scope,
        DateTimeOffset now,
        TimeSpan refreshBeforeExpiry,
        [NotNullWhen(true)] out BffStoredAccessToken? token
    )
    {
        foreach (var candidate in payload.AccessTokens)
        {
            if (!Matches(candidate, requirement, resource, scope))
            {
                continue;
            }

            if (candidate.ExpiresAt - refreshBeforeExpiry > now)
            {
                token = candidate;
                return true;
            }

            break;
        }

        token = null;
        return false;
    }

    /// <summary>
    /// Returns a payload that holds the specified token, replacing a matching entry or appending.
    /// </summary>
    /// <param name="payload">The session payload.</param>
    /// <param name="token">The token to store.</param>
    /// <returns>The updated payload.</returns>
    public static BffSessionPayload WithToken(BffSessionPayload payload, BffStoredAccessToken token)
    {
        var tokens = new List<BffStoredAccessToken>(payload.AccessTokens.Count + 1);
        var replaced = false;
        foreach (var existing in payload.AccessTokens)
        {
            if (Matches(existing, token.Requirement, token.Resource, token.Scope))
            {
                if (!replaced)
                {
                    tokens.Add(token);
                    replaced = true;
                }

                continue;
            }

            tokens.Add(existing);
        }

        if (!replaced)
        {
            tokens.Add(token);
        }

        while (tokens.Count > MaxEntries)
        {
            var oldest = 0;
            for (var i = 1; i < tokens.Count; i++)
            {
                if (tokens[i].ExpiresAt < tokens[oldest].ExpiresAt)
                {
                    oldest = i;
                }
            }

            tokens.RemoveAt(oldest);
        }

        return payload with
        {
            AccessTokens = tokens,
        };
    }

    private static bool Matches(
        BffStoredAccessToken candidate,
        string requirement,
        string? resource,
        string? scope
    ) =>
        string.Equals(candidate.Requirement, requirement, StringComparison.Ordinal)
        && string.Equals(candidate.Resource, resource, StringComparison.Ordinal)
        && string.Equals(candidate.Scope, scope, StringComparison.Ordinal);
}
