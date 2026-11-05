// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for the root key that a secret owner brings, which wraps the owner's
/// master keys. The complimentary DTO for this entity is <see cref="PersistedOwnerRootKey"/>.
/// </summary>
/// <remarks>
/// This entity is intentionally not tenant-scoped, like <see cref="MasterKeyEntity"/>: the store always filters by
/// owner explicitly. It holds the name of the key and the state of its last validation, never key material.
/// </remarks>
// See ADR-1414.
[Index(nameof(OwnerType), nameof(NormalizedOwnerId), IsUnique = true)]
public sealed class OwnerRootKeyEntity : ISupportConcurrencyToken
{
    /// <summary>
    /// Specifies the maximum length of <see cref="OwnerType"/>.
    /// </summary>
    private const int OwnerTypeMaxLength = 50;

    /// <summary>
    /// Specifies the maximum length of <see cref="State"/>.
    /// </summary>
    private const int StateMaxLength = 50;

    /// <summary>
    /// Specifies the maximum length of <see cref="KeyWrapperName"/>.
    /// </summary>
    private const int NameMaxLength = 100;

    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <summary>
    /// Gets the type of owner of this root key, such as <c>tenant</c>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OwnerTypeMaxLength)]
    public required string OwnerType { get; init; }

    /// <summary>
    /// Gets the identifier of the owner of this root key, as given.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="OwnerId"/> in a normalized form so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedOwnerId { get; init; }

    /// <summary>
    /// Gets or sets the name of the key wrapper that wraps under the root key.
    /// </summary>
    [Unicode(false)]
    [MaxLength(NameMaxLength)]
    public required string KeyWrapperName { get; set; }

    /// <summary>
    /// Gets or sets the reference to the root key that the owner gave.
    /// </summary>
    [Unicode(false)]
    [MaxLength(PersistedOwnerRootKey.MaxKeyReferenceLength)]
    public required string KeyReference { get; set; }

    /// <summary>
    /// Gets or sets the state of the root key.
    /// </summary>
    [Unicode(false)]
    [MaxLength(StateMaxLength)]
    public required string State { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when the current key reference was set.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when <see cref="State"/> last changed.
    /// </summary>
    public required DateTimeOffset StateChangedWhen { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> of the last successful probe, or <c>null</c>.
    /// </summary>
    public required DateTimeOffset? ValidatedWhen { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> of the last failed probe, or <c>null</c>.
    /// </summary>
    public required DateTimeOffset? FailedWhen { get; set; }

    /// <summary>
    /// Gets or sets the description of the last failure, or <c>null</c> after a success.
    /// </summary>
    [MaxLength(PersistedOwnerRootKey.MaxFailureLength)]
    public required string? Failure { get; set; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }
}
