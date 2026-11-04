// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for an <c>OAuth</c> or <c>OpenID Connect</c> server.
/// The complimentary DTO for this entity is <see cref="PersistedPlatform"/>.
/// </summary>
[Index(nameof(NormalizedPlatformId), IsUnique = true)]
public sealed class PlatformEntity : ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public long Id { get; init; }

    /// <summary>
    /// Gets the natural identifier for this entity.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string PlatformId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="PlatformId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedPlatformId { get; init; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token for server's settings.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string SettingsConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token for the server's secrets.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string SecretsConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the display name for the server.
    /// </summary>
    [Unicode]
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    // We never know how long the value will be.
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the server's settings.
    /// </summary>
    public required JsonElement SettingsJson { get; set; }

    /// <summary>
    /// Gets or sets the fingerprint of the server's effective settings that every tenant and client was last
    /// reconciled against, or <c>null</c> when they have not been reconciled since the server's settings last changed.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string? ReconciledSettingsFingerprint { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON of the server's effective settings that every tenant and client was last
    /// reconciled against, or <c>null</c> when there is nothing to compare a change with.
    /// </summary>
    public JsonElement? ReconciledSettingsJson { get; set; }

    // navigation properties

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    /// <summary>
    /// Gets the collection of secrets only known to this server.
    /// </summary>
    public required IEnumerable<PlatformSecretEntity> Secrets { get; init; }
}
