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
    /// <param name="scope">The session scope, whose selected frontend's registration requests the token.</param>
    /// <param name="request">The request, whose resource and scope the token is for.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The access token, or the failure.</returns>
    ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffSessionScope scope,
        BffTokenRequest request,
        CancellationToken cancellationToken
    );
}
