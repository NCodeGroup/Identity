// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the request to create a team, a grouping of related tenants.
/// </summary>
// See ADR-1411
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateTeamRequest
{
    /// <summary>
    /// Gets the identifier of the team. It follows the tenant-id form, 1 to 63 lowercase letters, digits or hyphens,
    /// and never changes.
    /// </summary>
    public required string TeamId { get; init; }

    /// <summary>
    /// Gets the display name for the team.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the team is disabled.
    /// </summary>
    public required bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the team's settings overrides as a JSON object of setting name to value.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
