// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The one place that resolves a scope's effective settings outside a materialized tenant or client: the platform's
/// settings, then each team's from the top-level team down, then the tenant's, then the client's or resource
/// server's, each merged onto the one before it with every
/// selected security profile's ceilings and defaults applied.
/// </summary>
/// <remarks>
/// Every one-shot merge uses this resolver instead of building the merge by hand, so a selected security profile is
/// never skipped. A host can replace the default implementation.
/// </remarks>
// See ADR-1204.
[PublicAPI]
public interface IEffectiveSettingsResolver
{
    /// <summary>
    /// Deserializes a scope's persisted settings document and expands it with the ceilings and defaults of every
    /// security profile it selects. The scope's environment type selects profiles too: the type is read from
    /// <paramref name="parentSettings"/> when the document leaves it unset.
    /// </summary>
    /// <param name="parentSettings">The effective settings of the scope's parent (the platform for a tenant, the tenant
    /// for a client or resource server).</param>
    /// <param name="scopeName">The name of the registered scope whose settings document <paramref name="settings"/>
    /// is; a setting that does not apply within it fails the read.</param>
    /// <param name="settings">The scope's own settings as a JSON document.</param>
    /// <returns>The deserialized, profile-expanded settings of the scope, not yet merged onto its parent.</returns>
    /// <exception cref="JsonException">The document cannot be honored, including a <c>security_profiles</c> value that
    /// names no registered profile.</exception>
    IReadOnlyCollection<Setting> ReadScopeSettings(
        IReadOnlySettingCollection parentSettings,
        string scopeName,
        JsonElement settings
    );

    /// <summary>
    /// Merges a scope's settings document onto the effective settings of its parent. It is equivalent to merging the
    /// result of <see cref="ReadScopeSettings"/> onto <paramref name="parentSettings"/>.
    /// </summary>
    /// <param name="parentSettings">The effective settings of the scope's parent (the platform for a tenant, the tenant
    /// for a client or resource server).</param>
    /// <param name="scopeName">The name of the registered scope whose settings document <paramref name="settings"/>
    /// is.</param>
    /// <param name="settings">The scope's own settings as a JSON document.</param>
    /// <returns>The effective settings of the scope.</returns>
    /// <exception cref="JsonException">The document cannot be honored, including a <c>security_profiles</c> value that
    /// names no registered profile.</exception>
    IReadOnlySettingCollection Resolve(
        IReadOnlySettingCollection parentSettings,
        string scopeName,
        JsonElement settings
    );

    /// <summary>
    /// Merges the settings document that a store holds for an owner of a scope (a tenant, a client or a resource
    /// server) onto its parent's effective settings, and names the document when it cannot be honored.
    /// </summary>
    /// <param name="parentSettings">The effective settings of the scope's parent (the platform for a tenant, the tenant
    /// for a client or resource server).</param>
    /// <param name="scopeName">The name of the registered scope whose settings document <paramref name="settings"/>
    /// is.</param>
    /// <param name="ownerId">The identifier of the tenant, client or resource server that owns the document.</param>
    /// <param name="settings">The owner's settings document as the store holds it.</param>
    /// <returns>The effective settings of the owner.</returns>
    /// <exception cref="UnreadableSettingsException">The document cannot be honored; the exception names the scope
    /// and the owner.</exception>
    // See ADR-1245.
    IReadOnlySettingCollection ResolveStored(
        IReadOnlySettingCollection parentSettings,
        string scopeName,
        string ownerId,
        JsonElement settings
    );

    /// <summary>
    /// Resolves a proposed platform settings document: the platform's configured settings merged with the supplied
    /// document.
    /// </summary>
    /// <param name="settings">The proposed platform settings as a JSON document.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>The effective settings of the platform if <paramref name="settings"/> were committed.</returns>
    /// <exception cref="JsonException">The document cannot be honored, including a <c>security_profiles</c> value that
    /// names no registered profile.</exception>
    ValueTask<IReadOnlySettingCollection> ResolvePlatformAsync(
        JsonElement settings,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves the platform's committed settings: the platform's configured settings merged with the platform's settings
    /// document that the store holds now, rather than the document this replica last polled.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>The effective settings of the platform as committed.</returns>
    /// <exception cref="UnreadableSettingsException">The committed document cannot be honored, including a
    /// <c>security_profiles</c> value that names no registered profile; the exception names the platform.</exception>
    // See ADR-1245.
    ValueTask<IReadOnlySettingCollection> ResolvePlatformAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a team's committed settings: the platform's committed effective settings with the settings document of
    /// each team above it merged on from the top-level team down, then the team's own settings document, each as the
    /// store holds it now. A tenant's parent is its team's effective settings.
    /// </summary>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>The effective settings of the team as committed.</returns>
    /// <exception cref="KeyNotFoundException">The team does not exist.</exception>
    /// <exception cref="UnreadableSettingsException">A committed document cannot be honored; the exception names the
    /// scope and the owner.</exception>
    // See ADR-1411.
    // See ADR-1426.
    ValueTask<IReadOnlySettingCollection> ResolveTeamAsync(
        string teamId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves the committed effective settings that a team's settings document merges onto: its parent team's
    /// effective settings, or the platform's committed effective settings for a top-level team.
    /// </summary>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>The effective settings of the team's parent as committed.</returns>
    /// <exception cref="KeyNotFoundException">The team does not exist.</exception>
    /// <exception cref="UnreadableSettingsException">A committed document above the team cannot be honored; the
    /// exception names the scope and the owner.</exception>
    // See ADR-1426.
    ValueTask<IReadOnlySettingCollection> ResolveTeamParentAsync(
        string teamId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves a proposed team settings document: the supplied document merged onto the team's parent's committed
    /// effective settings (see <see cref="ResolveTeamParentAsync"/>). A tenant's parent is its team's effective
    /// settings.
    /// </summary>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="settings">The proposed team settings as a JSON document.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>The effective settings of the team if <paramref name="settings"/> were committed.</returns>
    /// <exception cref="KeyNotFoundException">The team does not exist.</exception>
    /// <exception cref="JsonException">The document cannot be honored, including a <c>security_profiles</c> value that
    /// names no registered profile.</exception>
    // See ADR-1411.
    // See ADR-1426.
    ValueTask<IReadOnlySettingCollection> ResolveTeamAsync(
        string teamId,
        JsonElement settings,
        CancellationToken cancellationToken
    );
}
