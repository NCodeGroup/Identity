// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Identity.Json;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Builds a <see cref="PersistedTeam"/> for tests. Each property holds a value a test may care about and defaults to
/// the most common fixture value; <see cref="Build"/> copies the team identifier into the nested settings document,
/// whose concurrency token comes from <see cref="SettingsConcurrencyToken"/>. <see cref="BuildSettings"/> builds that
/// document alone.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PersistedTeamBuilder
{
    /// <summary>
    /// Gets the team's identifier. Defaults to <c>platform</c>.
    /// </summary>
    public string TeamId { get; init; } = "platform";

    /// <summary>
    /// Gets the identifier of the team directly above the team. Defaults to <see langword="null"/>, a top-level team.
    /// </summary>
    public string? ParentTeamId { get; init; }

    /// <summary>
    /// Gets the identifiers of the teams above the team. Defaults to an empty collection.
    /// </summary>
    public IReadOnlyList<string> AncestorTeamIds { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the team is disabled. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the display name of the team. Defaults to <see langword="null"/>, which builds as <see cref="TeamId"/>.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the concurrency token of the team. Defaults to an empty string.
    /// </summary>
    public string ConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concurrency token of the team's settings document. Defaults to an empty string.
    /// </summary>
    public string SettingsConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value of the team's settings document. Defaults to an empty JSON object.
    /// </summary>
    public JsonElement Settings { get; init; } = JsonElements.EmptyObject;

    /// <summary>
    /// Gets the fingerprint of the settings the team was last reconciled against. Defaults to
    /// <see langword="null"/>.
    /// </summary>
    public string? ReconciledSettingsFingerprint { get; init; }

    /// <summary>
    /// Gets the settings the team was last reconciled against. Defaults to <see langword="null"/>.
    /// </summary>
    public JsonElement? ReconciledSettings { get; init; }

    /// <summary>
    /// Builds the team's <see cref="PersistedTeamSettings"/> alone.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedTeamSettings"/> that carries the team identifier,
    /// <see cref="SettingsConcurrencyToken"/> and <see cref="Settings"/>.
    /// </returns>
    public PersistedTeamSettings BuildSettings() =>
        new()
        {
            TeamId = TeamId,
            ConcurrencyToken = SettingsConcurrencyToken,
            Value = Settings,
        };

    /// <summary>
    /// Builds the <see cref="PersistedTeam"/>.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedTeam"/> whose settings come from <see cref="BuildSettings"/> and whose ancestor
    /// identifiers are a copy of <see cref="AncestorTeamIds"/>.
    /// </returns>
    public PersistedTeam Build() =>
        new()
        {
            TeamId = TeamId,
            ConcurrencyToken = ConcurrencyToken,
            ParentTeamId = ParentTeamId,
            AncestorTeamIds = [.. AncestorTeamIds],
            IsDisabled = IsDisabled,
            DisplayName = DisplayName ?? TeamId,
            Settings = BuildSettings(),
            ReconciledSettingsFingerprint = ReconciledSettingsFingerprint,
            ReconciledSettings = ReconciledSettings,
        };
}
