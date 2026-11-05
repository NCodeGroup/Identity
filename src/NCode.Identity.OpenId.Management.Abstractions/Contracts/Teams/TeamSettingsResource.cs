// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedTeamSettings"/> instance.
/// </summary>
// See ADR-1411
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TeamSettingsResource : ISupportTeamId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTeamId.TeamId"/>
    public required string TeamId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <inheritdoc cref="PersistedTeamResource{TValue}.Value"/>
    public required JsonElement Settings { get; init; }
}
