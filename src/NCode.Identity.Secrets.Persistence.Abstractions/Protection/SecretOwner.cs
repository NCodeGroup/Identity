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

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Identifies the owner of a set of persisted secrets by the kind of owner and the identifier of the owner within
/// that kind, for example a tenant or the server itself.
/// </summary>
[PublicAPI]
public readonly record struct SecretOwner
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretOwner"/> struct.
    /// </summary>
    /// <param name="ownerType">The kind of owner, such as one of the values in <see cref="SecretOwnerTypes"/>.</param>
    /// <param name="ownerId">The identifier of the owner within its kind.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="ownerType"/> or <paramref name="ownerId"/> is <see langword="null"/>, empty or
    /// whitespace.
    /// </exception>
    public SecretOwner(string ownerType, string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        OwnerType = ownerType;
        OwnerId = ownerId;
    }

    /// <summary>
    /// Gets the kind of owner, such as one of the values in <see cref="SecretOwnerTypes"/>.
    /// </summary>
    public string OwnerType { get; }

    /// <summary>
    /// Gets the identifier of the owner within its kind.
    /// </summary>
    public string OwnerId { get; }

    /// <summary>
    /// Creates the owner that represents the server as a whole.
    /// </summary>
    /// <param name="serverId">The identifier of the server.</param>
    /// <returns>The <see cref="SecretOwner"/> for the server.</returns>
    public static SecretOwner ForServer(string serverId) => new(SecretOwnerTypes.Server, serverId);

    /// <summary>
    /// Creates the owner that represents a single tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <returns>The <see cref="SecretOwner"/> for the tenant.</returns>
    public static SecretOwner ForTenant(string tenantId) => new(SecretOwnerTypes.Tenant, tenantId);

    /// <summary>
    /// Creates a copy of this owner with both the owner type and the owner identifier converted to lower case using
    /// the invariant culture.
    /// </summary>
    /// <returns>The normalized <see cref="SecretOwner"/>.</returns>
    public SecretOwner Normalize() => new(OwnerType.ToLowerInvariant(), OwnerId.ToLowerInvariant());

    /// <summary>
    /// Returns the owner as the owner type and the owner identifier joined by a colon.
    /// </summary>
    /// <returns>The owner type and the owner identifier separated by a colon.</returns>
    public override string ToString() => $"{OwnerType}:{OwnerId}";
}
