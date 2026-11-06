// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedTeam"/> instance, a grouping of related tenants.
/// </summary>
// See ADR-1411
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TeamResource : ISupportTeamId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTeamId.TeamId"/>
    public required string TeamId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <inheritdoc cref="PersistedTeam.DisplayName"/>
    public required string DisplayName { get; init; }

    /// <inheritdoc cref="PersistedTeam.IsDisabled"/>
    public required bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the identifier of the team directly above this team, or <see langword="null"/> for a top-level team.
    /// </summary>
    // See ADR-1426.
    public string? ParentTeamId { get; init; }
}
