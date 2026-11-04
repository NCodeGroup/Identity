// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the REST resource for a team's <b>effective</b> (merged) settings: the platform baseline, the team's
/// persisted overrides, and descriptor defaults, resolved through the settings merge pipeline.
/// </summary>
// See ADR-1403
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TeamEffectiveSettingsResource : ISupportTeamId
{
    /// <inheritdoc cref="ISupportTeamId.TeamId"/>
    public required string TeamId { get; init; }

    /// <summary>
    /// Gets the team's effective settings as a flat JSON object of setting name to formatted value, using the same
    /// value representation as the discovery document.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
