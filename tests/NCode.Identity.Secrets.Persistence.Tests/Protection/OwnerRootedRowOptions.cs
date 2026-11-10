// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the optional inputs of a master key row that a test creates under an owner root key.
/// </summary>
public sealed class OwnerRootedRowOptions
{
    /// <summary>
    /// Gets the version of the row.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets the key reference of the owner root key, or <see langword="null"/> for the default reference of the tests.
    /// </summary>
    public string? KeyReference { get; init; }

    /// <summary>
    /// Gets the access identity of the owner root key, or <see langword="null"/> for none.
    /// </summary>
    public string? AccessIdentity { get; init; }
}
