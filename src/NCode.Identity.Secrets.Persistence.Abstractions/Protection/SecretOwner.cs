// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Identifies the owner of a set of persisted secrets by the kind of owner and the identifier of the owner within
/// that kind, for example a tenant or the platform.
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
    /// Contains the identifier of the platform owner.
    /// </summary>
    public const string PlatformOwnerId = "platform";

    /// <summary>
    /// Gets the owner of the one master key that protects the Data Protection key ring. The platform owns no stored
    /// secret.
    /// </summary>
    public static SecretOwner Platform => new(SecretOwnerTypes.Platform, PlatformOwnerId);

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
