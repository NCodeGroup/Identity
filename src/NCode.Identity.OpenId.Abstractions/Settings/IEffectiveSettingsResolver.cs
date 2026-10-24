#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The one place that resolves a scope's effective settings outside a materialized tenant or client: the server's
/// settings, then the tenant's, then the client's or resource server's, each merged onto the one before it with every
/// selected security profile's ceilings and defaults applied.
/// </summary>
/// <remarks>
/// Every one-shot merge uses this resolver instead of building the merge by hand, so a selected security profile is
/// never skipped. A host can replace the default implementation.
/// </remarks>
// See ADR-0204.
[PublicAPI]
public interface IEffectiveSettingsResolver
{
    /// <summary>
    /// Deserializes a scope's persisted settings document and expands it with the ceilings and defaults of every
    /// security profile it selects.
    /// </summary>
    /// <param name="scopeName">The name of the registered scope whose settings document <paramref name="settings"/>
    /// is; a setting that does not apply within it fails the read.</param>
    /// <param name="settings">The scope's own settings as a JSON document.</param>
    /// <returns>The deserialized, profile-expanded settings of the scope, not yet merged onto its parent.</returns>
    /// <exception cref="JsonException">The document cannot be honored, including a <c>security_profiles</c> value that
    /// names no registered profile.</exception>
    IReadOnlyCollection<Setting> ReadScopeSettings(string scopeName, JsonElement settings);

    /// <summary>
    /// Merges a scope's settings document onto the effective settings of its parent. It is equivalent to merging the
    /// result of <see cref="ReadScopeSettings"/> onto <paramref name="parentSettings"/>.
    /// </summary>
    /// <param name="parentSettings">The effective settings of the scope's parent (the server for a tenant, the tenant
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
    /// <param name="parentSettings">The effective settings of the scope's parent (the server for a tenant, the tenant
    /// for a client or resource server).</param>
    /// <param name="scopeName">The name of the registered scope whose settings document <paramref name="settings"/>
    /// is.</param>
    /// <param name="ownerId">The identifier of the tenant, client or resource server that owns the document.</param>
    /// <param name="settings">The owner's settings document as the store holds it.</param>
    /// <returns>The effective settings of the owner.</returns>
    /// <exception cref="UnreadableSettingsException">The document cannot be honored; the exception names the scope
    /// and the owner.</exception>
    // See ADR-0245.
    IReadOnlySettingCollection ResolveStored(
        IReadOnlySettingCollection parentSettings,
        string scopeName,
        string ownerId,
        JsonElement settings
    );

    /// <summary>
    /// Resolves a proposed server settings document: the server's configured settings merged with the supplied
    /// document.
    /// </summary>
    /// <param name="settings">The proposed server settings as a JSON document.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>The effective settings of the server if <paramref name="settings"/> were committed.</returns>
    /// <exception cref="JsonException">The document cannot be honored, including a <c>security_profiles</c> value that
    /// names no registered profile.</exception>
    ValueTask<IReadOnlySettingCollection> ResolveServerAsync(
        JsonElement settings,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves the server's committed settings: the server's configured settings merged with the server's settings
    /// document that the store holds now, rather than the document this replica last polled.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>The effective settings of the server as committed.</returns>
    /// <exception cref="UnreadableSettingsException">The committed document cannot be honored, including a
    /// <c>security_profiles</c> value that names no registered profile; the exception names the server.</exception>
    // See ADR-0245.
    ValueTask<IReadOnlySettingCollection> ResolveServerAsync(CancellationToken cancellationToken);
}
