// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides one way of obtaining an access token for a request. Requirements form an open strategy collection that is
/// selected by <see cref="Name"/>.
/// </summary>
[PublicAPI]
public interface IBffTokenRequirement
{
    /// <summary>
    /// Gets the name that selects this requirement. Names are compared ordinally.
    /// </summary>
    string Name { get; }

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
