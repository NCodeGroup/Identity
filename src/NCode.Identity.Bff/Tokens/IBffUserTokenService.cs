// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Provides the access token of the <c>user</c> token requirement from the session of a scope.
/// </summary>
internal interface IBffUserTokenService
{
    /// <summary>
    /// Gets an access token for the user of the scope, refreshing it when it is stale.
    /// </summary>
    /// <param name="scope">The session scope, a request or a connection.</param>
    /// <param name="request">The request, whose resource and scope the token is for.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffSessionScope scope,
        BffTokenRequest request,
        CancellationToken cancellationToken
    );
}
