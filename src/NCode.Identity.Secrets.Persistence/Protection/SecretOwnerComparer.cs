#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Compares <see cref="SecretOwner"/> values without regard to the case of the owner type and the owner identifier,
/// so that a lookup does not have to allocate a normalized copy of the owner.
/// </summary>
// See ADR-0228.
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
