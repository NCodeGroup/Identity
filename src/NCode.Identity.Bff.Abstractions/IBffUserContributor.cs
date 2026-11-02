// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Adds computed fields to the <c>/bff/user</c> response, beside its reserved keys, on every request for it. Every
/// registered contributor runs, in registration order.
/// </summary>
// See ADR-1290.
[PublicAPI]
public interface IBffUserContributor
{
    /// <summary>
    /// Adds fields to the response.
    /// </summary>
    /// <param name="context">The context of the request, which collects the fields.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the contributor is done.</returns>
    ValueTask ContributeAsync(BffUserContext context, CancellationToken cancellationToken);
}
