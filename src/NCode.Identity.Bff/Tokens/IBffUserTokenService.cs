// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Provides the access token of the <c>user</c> token requirement from the session of a request.
/// </summary>
internal interface IBffUserTokenService
{
    /// <summary>
    /// Gets an access token for the user of the request, refreshing it when it is stale.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="request">The request, whose resource and scope the token is for.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        BffTokenRequest request,
        CancellationToken cancellationToken
    );
}
