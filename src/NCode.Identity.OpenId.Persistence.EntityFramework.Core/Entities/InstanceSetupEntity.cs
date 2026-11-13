// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for the persisted setup state of an instance. The complimentary DTO
/// for this entity is <see cref="PersistedInstanceSetup"/>.
/// </summary>
/// <remarks>
/// This entity is intentionally not tenant-scoped: an instance has one row, keyed by
/// <see cref="PersistedInstanceSetup.SingletonKey"/>, that every replica reads. It holds only the hash of the setup
/// token, never the token itself.
/// </remarks>
public sealed class InstanceSetupEntity : ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the natural key for this entity, always <see cref="PersistedInstanceSetup.SingletonKey"/>: the primary
    /// key on it keeps the table to one row.
    /// </summary>
    [Key]
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string Key { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the instance is initialized, which is when its first administrator has
    /// been created.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the current setup token, or <c>null</c> when no token is stored.
    /// </summary>
    [MaxLength(PersistedInstanceSetup.TokenHashLength)]
    public byte[]? TokenHash { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> at which the current setup token was issued, or <c>null</c> when
    /// no token is stored.
    /// </summary>
    public DateTimeOffset? TokenIssuedAt { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> at which the current setup token expires, or <c>null</c> when no
    /// token is stored.
    /// </summary>
    public DateTimeOffset? TokenExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> at which the instance was initialized, or <c>null</c> when it is
    /// not initialized.
    /// </summary>
    public DateTimeOffset? InitializedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that was created as the first administrator, or <c>null</c> when
    /// the instance is not initialized.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.PrincipalId)]
    public string? InitializedByPrincipalId { get; set; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }
}
