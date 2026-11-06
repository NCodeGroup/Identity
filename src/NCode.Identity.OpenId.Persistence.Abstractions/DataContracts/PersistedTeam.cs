// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted team: a grouping of related tenants, such as a customer's sandbox, testing and
/// production tenants, for billing and administration. A team's settings sit between the platform's and its tenants'
/// in the settings cascade. A team can sit beneath a parent team, and its settings sit beneath its parent's in the
/// cascade.
/// </summary>
// See ADR-1411
// See ADR-1426.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedTeam : BasePersistedTeamResource
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType => ResourceTypePrefix;

    /// <summary>
    /// Gets or sets a value indicating whether the team is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the team directly above this one, or <c>null</c> for a top-level team. It is set
    /// when the team is created, because a team never moves.
    /// </summary>
    public string? ParentTeamId { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the teams above this one, from its top-level team down to its parent. It is
    /// empty for a top-level team. The store fills it from the team's path.
    /// </summary>
    public IReadOnlyList<string> AncestorTeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the display name for the team.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the JSON settings for an OpenID Team instance.
    /// </summary>
    public required PersistedTeamSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the fingerprint of the effective settings that the team's tenants and clients were last reconciled
    /// against, or <c>null</c> when they have not been reconciled.
    /// </summary>
    public string? ReconciledSettingsFingerprint { get; set; }

    /// <summary>
    /// Gets or sets the effective settings that the team's tenants and clients were last reconciled against, or
    /// <c>null</c> when they have not been reconciled.
    /// </summary>
    public JsonElement? ReconciledSettings { get; set; }
}
