// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// The rule that decides which algorithms a tenant uses, and so which keys the tenant needs: an algorithm is used when
/// some named algorithm setting of the tenant's effective settings lists it.
/// </summary>
// See ADR-1169 (decision 9).
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
