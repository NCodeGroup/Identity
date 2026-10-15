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
/// Expands a scope's own settings according to its selected <c>security_profile</c> (for example <c>fapi2</c>): when a
/// named profile is selected, the profile's ceiling settings are merged into the scope's settings so the existing
/// floor/ceiling merge lattice carries the profile down the <c>server → tenant → client</c> cascade un-loosenably, and
/// both discovery and request-time enforcement read one source of truth.
/// </summary>
[PublicAPI]
public interface ISecurityProfileSettingsExpander
{
    /// <summary>
    /// Returns the supplied <paramref name="scopeSettings"/> expanded with the ceiling settings of the
    /// <c>security_profile</c> it declares, or the settings unchanged when it declares no recognized profile.
    /// </summary>
    /// <param name="scopeSettings">The scope's own (not-yet-cascaded) settings.</param>
    /// <returns>The expanded settings.</returns>
    IReadOnlyCollection<Setting> Expand(IReadOnlyCollection<Setting> scopeSettings);

    /// <summary>
    /// Deserializes a scope's persisted settings document and expands it per the <c>security_profile</c> it declares —
    /// the single operation every persisted-scope load uses so the profile is always applied.
    /// </summary>
    /// <param name="settingsJson">The scope's persisted settings as a JSON document.</param>
    /// <param name="jsonOptions">The <see cref="JsonSerializerOptions"/> used to deserialize the settings.</param>
    /// <returns>The deserialized, profile-expanded settings.</returns>
    IReadOnlyCollection<Setting> Expand(
        JsonElement settingsJson,
        JsonSerializerOptions jsonOptions
    );
}
