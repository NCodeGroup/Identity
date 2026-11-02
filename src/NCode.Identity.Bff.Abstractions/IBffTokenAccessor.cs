// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff;

/// <summary>
/// Obtains an access token for a request by dispatching to the <see cref="IBffTokenRequirement"/> that the request names.
/// </summary>
[PublicAPI]
public interface IBffTokenAccessor
{
    /// <summary>
    /// Gets an access token for the request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="request">The token request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The token or the reason that none is available.</returns>
    ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        BffTokenRequest request,
        CancellationToken cancellationToken
    );
}
