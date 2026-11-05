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
/// Represents an entity framework data contract for a team, the group of tenants that share one set of team-level
/// settings. The complimentary DTO for this entity is <see cref="PersistedTeam"/>.
/// </summary>
// See ADR-1411
[Index(nameof(NormalizedTeamId), IsUnique = true)]
public sealed class TeamEntity : ISupportConcurrencyToken
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
    public required string TeamId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="TeamId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedTeamId { get; init; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token for the team's settings.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string SettingsConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the team is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the display name for the team.
    /// </summary>
    [Unicode]
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    // We never know how long the value will be.
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the team settings.
    /// </summary>
    public required JsonElement SettingsJson { get; set; }

    /// <summary>
    /// Gets or sets the fingerprint of the settings the team's tenants were last reconciled against.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string? ReconciledSettingsFingerprint { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON of the settings the team's tenants were last reconciled against.
    /// </summary>
    public JsonElement? ReconciledSettingsJson { get; set; }

    // navigation properties

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    /// <summary>
    /// Gets the collection of tenants that belong to this team.
    /// </summary>
    public required IEnumerable<TenantEntity> Tenants { get; init; }
}
