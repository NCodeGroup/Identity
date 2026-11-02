// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Decides whether a validated login may create a session.
/// </summary>
/// <remarks>
/// Gates run after the ID token validates and before a session exists. A gate can only reject a login; every
/// registered gate must allow it.
/// </remarks>
[PublicAPI]
public interface IBffLoginGate
{
    /// <summary>
    /// Decides whether the login may create a session.
    /// </summary>
    /// <param name="context">The context of the login.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> to allow the login; otherwise, <see langword="false"/> to reject it.</returns>
    ValueTask<bool> AllowAsync(BffLoginGateContext context, CancellationToken cancellationToken);
}
