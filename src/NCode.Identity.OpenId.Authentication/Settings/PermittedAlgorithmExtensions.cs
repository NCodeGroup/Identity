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

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Settings;

/// <summary>
/// Reads an algorithm list (<c>*_alg_values_supported</c>, <c>*_enc_values_supported</c>) where it decides which
/// algorithms the server accepts from another party — a cryptographic trust boundary.
/// </summary>
// See ADR-0155.
internal static class PermittedAlgorithmExtensions
{
    extension(IReadOnlySettingCollection settings)
    {
        /// <summary>
        /// Gets the algorithms a setting permits at a trust boundary: the list the setting holds, or none when it is
        /// unset. The root settings always supply each list from the registered algorithms, so an unset list can only
        /// mean a list was removed; it never widens acceptance to every algorithm a JOSE implementation registers.
        /// </summary>
        /// <param name="settingKey">The key of the algorithm list.</param>
        /// <returns>The permitted algorithms; empty when the setting is unset.</returns>
        public IReadOnlyCollection<string> GetPermittedAlgorithms(
            SettingKey<IReadOnlyCollection<string>> settingKey
        ) => settings.TryGetValue(settingKey, out var permitted) ? permitted : [];
    }
}
