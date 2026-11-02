// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Shapes the claims of a session once, at login: after the ID token validates and before the login gates run, so a
/// gate judges the session as it will be created. A good place for an expensive lookup, such as roles from a
/// directory, because the session keeps the result. Every registered contributor runs, in registration order.
/// </summary>
// See ADR-1290.
[PublicAPI]
public interface IBffSessionClaimContributor
{
    /// <summary>
    /// Adds or removes claims of the session about to be created.
    /// </summary>
    /// <param name="context">The context of the login, which holds the claims.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the contributor is done.</returns>
    ValueTask ContributeAsync(BffSessionClaimContext context, CancellationToken cancellationToken);
}
