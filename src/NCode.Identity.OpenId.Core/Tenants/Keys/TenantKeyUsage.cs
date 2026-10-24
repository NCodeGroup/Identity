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

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// The rule that decides which algorithms a tenant uses, and so which keys the tenant needs: an algorithm is used when
/// some named algorithm setting of the tenant's effective settings lists it.
/// </summary>
// See ADR-0169 (decision 9).
internal static class TenantKeyUsage
{
    /// <summary>
    /// Determines whether the effective settings list the algorithm under any of the named algorithm settings.
    /// </summary>
    /// <param name="algorithmSettings">The names of the settings whose lists name the algorithms a tenant uses.</param>
    /// <param name="settings">The effective settings to read.</param>
    /// <param name="algorithm">The algorithm to look for.</param>
    /// <returns><see langword="true"/> when a named setting lists the algorithm; otherwise, <see langword="false"/>.</returns>
    public static bool IsUsed(
        IEnumerable<string> algorithmSettings,
        IReadOnlySettingCollection settings,
        string algorithm
    ) =>
        algorithmSettings.Any(settingName =>
            ReadAlgorithms(settings, settingName).Contains(algorithm, StringComparer.Ordinal)
        );

    private static IEnumerable<string> ReadAlgorithms(
        IReadOnlySettingCollection settings,
        string settingName
    ) => TryReadAlgorithms(settings, settingName, out var list) ? list : [];

    private static bool TryReadAlgorithms(
        IReadOnlySettingCollection settings,
        string settingName,
        out IEnumerable<string> list
    )
    {
        if (settings.TryGetValue(settingName, out var value) && value is IEnumerable<string> found)
        {
            list = found;
            return true;
        }

        list = [];
        return false;
    }
}
