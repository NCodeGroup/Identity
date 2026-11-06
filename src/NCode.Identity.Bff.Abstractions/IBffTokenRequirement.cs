// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides one way of obtaining an access token for a request or a connection. Requirements form an open strategy
/// collection that is selected by <see cref="Name"/>.
/// </summary>
[PublicAPI]
public interface IBffTokenRequirement
{
    /// <summary>
    /// Gets the name that selects this requirement. Names are compared ordinally.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an access token for the context.
    /// </summary>
    /// <param name="context">The request or connection that obtains the token.</param>
    /// <param name="request">The token request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The token or the reason that none is available.</returns>
    ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffTokenContext context,
        BffTokenRequest request,
        CancellationToken cancellationToken
    );
}
