// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Provides the access token of the <c>exchange</c> token requirement from the session of a request.
/// </summary>
internal interface IBffExchangeTokenService
{
    /// <summary>
    /// Gets an access token that the provider issues for the user's access token through token exchange (RFC 8693),
    /// exchanging it again when the cached one is stale.
    /// </summary>
    /// <param name="scope">The session scope the token is for.</param>
    /// <param name="request">The request, whose resource and scope the token is for.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The access token, or the failure.</returns>
    ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffSessionScope scope,
        BffTokenRequest request,
        CancellationToken cancellationToken
    );
}
