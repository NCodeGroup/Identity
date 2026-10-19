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
/// turns the profiles a scope selects through <c>security_profiles</c> into both their ceiling settings (expanded into
/// the merge lattice) and their request-time <see cref="ISecurityProfileRequirement"/> objects (resolved by the
/// enforcement sites).
/// </summary>
/// <remarks>
/// Every selected profile applies: the catalog visits the selected profiles in the ordinal order of their names, and a
/// selected name that matches no registered profile is an error rather than a profile that is skipped.
/// </remarks>
[PublicAPI]
public interface ISecurityProfileCatalog
{
    /// <summary>
    /// Attempts to get the registered profile selected by the specified <paramref name="profileName"/>.
    /// </summary>
    /// <param name="profileName">The <c>security_profiles</c> value to look up (for example <c>fapi2</c>).</param>
    /// <param name="profile">When this method returns, contains the matching profile, if one is registered.</param>
    /// <returns><c>true</c> if a profile is registered for the name; otherwise, <c>false</c>.</returns>
    bool TryGetProfile(string profileName, [MaybeNullWhen(false)] out ISecurityProfile profile);

    /// <summary>
    /// Runs the selected profiles' requirements for a single enforcement phase: it resolves every profile the supplied
    /// <paramref name="settings"/> select (via their effective <c>security_profiles</c> value) and invokes each of their
    /// requirements assignable to <c>ISecurityProfileRequirement&lt;TContext&gt;</c> against <paramref name="context"/>,
    /// profile by profile and in each profile's order, evaluating a requirement instance shared by several profiles
    /// once. A requirement throws to reject the request; this method does nothing when the settings select no profile
    /// or no selected profile imposes a requirement for the phase.
    /// </summary>
    /// <typeparam name="TContext">The phase-specific evaluation context type, which selects the applicable requirements.</typeparam>
    /// <param name="settings">The effective settings of the scope (typically a client) to evaluate.</param>
    /// <param name="context">The phase-specific evaluation context passed to each requirement.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <exception cref="InvalidOperationException">The settings select a profile that is not registered; the scope is
    /// never treated as if it had not selected it.</exception>
    ValueTask EvaluateRequirementsAsync<TContext>(
        IReadOnlySettingCollection settings,
        TContext context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Returns the supplied <paramref name="scopeSettings"/> expanded with the ceiling settings of every profile the
    /// scope declares in <c>security_profiles</c> — including a profile that another profile's ceiling selects — or the
    /// settings unchanged when it declares none.
    /// </summary>
    /// <param name="scopeSettings">The scope's own (not-yet-cascaded) settings.</param>
    /// <returns>The expanded settings.</returns>
    /// <exception cref="InvalidOperationException">The settings declare a profile that is not registered.</exception>
    IReadOnlyCollection<Setting> Expand(IReadOnlyCollection<Setting> scopeSettings);

    /// <summary>
    /// Deserializes a scope's persisted settings document and expands it per the <c>security_profiles</c> it declares —
    /// the single operation every persisted-scope load uses so the selected profiles are always applied.
    /// </summary>
    /// <param name="settingsJson">The scope's persisted settings as a JSON document.</param>
    /// <param name="scopeName">The name of the registered scope whose settings document <paramref name="settingsJson"/>
    /// is; a setting that does not apply within it fails the load.</param>
    /// <param name="jsonOptions">The <see cref="JsonSerializerOptions"/> used to deserialize the settings.</param>
    /// <returns>The deserialized, profile-expanded settings.</returns>
    /// <exception cref="JsonException">The document cannot be honored, including a <c>security_profiles</c> value that
    /// names no registered profile.</exception>
    IReadOnlyCollection<Setting> Expand(
        JsonElement settingsJson,
        string scopeName,
        JsonSerializerOptions jsonOptions
    );
}
