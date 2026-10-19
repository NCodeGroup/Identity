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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The registry of the <see cref="ISecurityProfile"/> implementations known to the OP. It is the single place that
/// turns a scope's selected <c>security_profile</c> into both its ceiling settings (expanded into the merge lattice)
/// and its request-time <see cref="ISecurityProfileRequirement"/> objects (resolved by the enforcement sites).
/// </summary>
[PublicAPI]
public interface ISecurityProfileCatalog
{
    /// <summary>
    /// Attempts to get the registered profile activated by the specified <paramref name="profileName"/>.
    /// </summary>
    /// <param name="profileName">The <c>security_profile</c> value to look up (for example <c>fapi2</c>).</param>
    /// <param name="profile">When this method returns, contains the matching profile, if one is registered.</param>
    /// <returns><c>true</c> if a profile is registered for the name; otherwise, <c>false</c>.</returns>
    bool TryGetProfile(string profileName, [MaybeNullWhen(false)] out ISecurityProfile profile);

    /// <summary>
    /// Runs the active profile's requirements for a single enforcement phase: it resolves the profile the supplied
    /// <paramref name="settings"/> select (via their effective <c>security_profile</c> value) and invokes each of its
    /// requirements assignable to <c>ISecurityProfileRequirement&lt;TContext&gt;</c> against <paramref name="context"/>,
    /// in order. A requirement throws to reject the request; this method does nothing when the settings select no
    /// profile or the profile imposes no requirement for the phase.
    /// </summary>
    /// <typeparam name="TContext">The phase-specific evaluation context type, which selects the applicable requirements.</typeparam>
    /// <param name="settings">The effective settings of the scope (typically a client) to evaluate.</param>
    /// <param name="context">The phase-specific evaluation context passed to each requirement.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <exception cref="InvalidOperationException">The settings select a profile that is not registered; the scope is
    /// never treated as if it selected none.</exception>
    ValueTask EvaluateRequirementsAsync<TContext>(
        IReadOnlySettingCollection settings,
        TContext context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Returns the supplied <paramref name="scopeSettings"/> expanded with the ceiling settings of the
    /// <c>security_profile</c> it declares, or the settings unchanged when it declares no profile.
    /// </summary>
    /// <param name="scopeSettings">The scope's own (not-yet-cascaded) settings.</param>
    /// <returns>The expanded settings.</returns>
    /// <exception cref="InvalidOperationException">The settings declare a profile that is not registered.</exception>
    IReadOnlyCollection<Setting> Expand(IReadOnlyCollection<Setting> scopeSettings);

    /// <summary>
    /// Deserializes a scope's persisted settings document and expands it per the <c>security_profile</c> it declares —
    /// the single operation every persisted-scope load uses so the profile is always applied.
    /// </summary>
    /// <param name="settingsJson">The scope's persisted settings as a JSON document.</param>
    /// <param name="jsonOptions">The <see cref="JsonSerializerOptions"/> used to deserialize the settings.</param>
    /// <returns>The deserialized, profile-expanded settings.</returns>
    /// <exception cref="JsonException">The document cannot be honored, including a <c>security_profile</c> that names
    /// no registered profile.</exception>
    IReadOnlyCollection<Setting> Expand(
        JsonElement settingsJson,
        JsonSerializerOptions jsonOptions
    );
}
