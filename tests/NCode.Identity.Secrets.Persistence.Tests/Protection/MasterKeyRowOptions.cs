// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the optional inputs of a master key row that a test creates under the ephemeral root key.
/// </summary>
public sealed class MasterKeyRowOptions
{
    /// <summary>
    /// Gets the version of the row.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets a value indicating whether the row is retired.
    /// </summary>
    public bool Retired { get; init; }

    /// <summary>
    /// Gets the normalized owner of the row, or <see langword="null"/> for the owner of the tests.
    /// </summary>
    public SecretOwner? NormalizedOwner { get; init; }
}
