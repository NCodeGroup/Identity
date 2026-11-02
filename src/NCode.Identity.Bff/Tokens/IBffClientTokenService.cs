// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Provides the BFF's own access token, from the <c>client_credentials</c> grant.
/// </summary>
internal interface IBffClientTokenService
{
    /// <summary>
    /// Gets the BFF's own access token for a resource and scope, requesting a new one when the cached one is stale.
    /// </summary>
    /// <param name="resource">The resource indicator, or <see langword="null"/>.</param>
    /// <param name="scope">The scope, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The access token, or the failure.</returns>
    ValueTask<BffTokenResult> GetAccessTokenAsync(
        string? resource,
        string? scope,
        CancellationToken cancellationToken
    );
}
