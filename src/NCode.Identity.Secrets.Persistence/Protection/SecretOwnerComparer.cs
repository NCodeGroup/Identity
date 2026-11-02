// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Compares <see cref="SecretOwner"/> values without regard to the case of the owner type and the owner identifier,
/// so that a lookup does not have to allocate a normalized copy of the owner.
/// </summary>
// See ADR-1235.
internal sealed class SecretOwnerComparer : IEqualityComparer<SecretOwner>
{
    /// <summary>
    /// Gets the shared instance of the comparer, which has no state.
    /// </summary>
    public static SecretOwnerComparer Instance { get; } = new();

    private SecretOwnerComparer() { }

    /// <inheritdoc />
    public bool Equals(SecretOwner x, SecretOwner y) =>
        StringComparer.OrdinalIgnoreCase.Equals(x.OwnerType, y.OwnerType)
        && StringComparer.OrdinalIgnoreCase.Equals(x.OwnerId, y.OwnerId);

    /// <inheritdoc />
    public int GetHashCode(SecretOwner obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.OwnerType),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.OwnerId)
        );
}
