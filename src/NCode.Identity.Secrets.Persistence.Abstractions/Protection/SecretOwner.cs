// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    /// Gets the identifier of the tenant when the owner is a tenant, which is the owner identifier of an owner whose
    /// type is <see cref="SecretOwnerTypes.Tenant"/> compared without regard to case; otherwise <see langword="null"/>.
    /// </summary>
    public string? TenantId =>
        string.Equals(OwnerType, SecretOwnerTypes.Tenant, StringComparison.OrdinalIgnoreCase)
            ? OwnerId
            : null;

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
    /// Parses an owner from the text that <see cref="ToString"/> produces: the owner type and the owner identifier
    /// separated by the first colon.
    /// </summary>
    /// <param name="value">The text to parse.</param>
    /// <param name="owner">The parsed owner, when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when both parts are present and not blank; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out SecretOwner owner)
    {
        owner = default;

        var separator = value?.IndexOf(':') ?? -1;
        if (value is null || separator < 0)
            return false;

        var ownerType = value[..separator];
        var ownerId = value[(separator + 1)..];
        if (string.IsNullOrWhiteSpace(ownerType) || string.IsNullOrWhiteSpace(ownerId))
            return false;

        owner = new SecretOwner(ownerType, ownerId);
        return true;
    }

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
