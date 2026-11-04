// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the mutable metadata of a team that a JSON Patch document may modify. Settings are managed through
/// their own endpoints.
/// </summary>
// See ADR-1403
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateTeamRequest
{
    /// <summary>
    /// Gets or sets the display name for the team.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the team is disabled.
    /// </summary>
    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }
}
