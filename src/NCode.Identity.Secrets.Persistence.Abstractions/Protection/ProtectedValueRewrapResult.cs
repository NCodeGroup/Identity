// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Reports the outcome of a pass of an <see cref="IProtectedValueSource"/> that rewraps protected values.
/// </summary>
/// <param name="Rewrapped">The number of values that were written under the active master key.</param>
/// <param name="Failed">The number of values that name a requested master key and could not be rewrapped. They still
/// name the key, so the key is kept.</param>
// See ADR-1407.
[PublicAPI]
public sealed record ProtectedValueRewrapResult(int Rewrapped, int Failed)
{
    /// <summary>
    /// Gets the result of a pass that rewrapped nothing and failed nothing.
    /// </summary>
    public static ProtectedValueRewrapResult None { get; } = new(0, 0);
}
