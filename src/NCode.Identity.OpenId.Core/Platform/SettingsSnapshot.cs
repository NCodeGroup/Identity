// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Writes a server's effective settings as the JSON object that records what every tenant and client was reconciled
/// against: each setting's name and its value as the setting serializer reads it back, in ordinal order of the names.
/// </summary>
// See ADR-1234.
internal static class SettingsSnapshot
{
    /// <summary>
    /// Writes the specified <paramref name="settings"/> as a JSON object of each setting's name and value.
    /// </summary>
    /// <param name="settings">The effective settings to record.</param>
    /// <param name="jsonOptions">The JSON options that serialize each setting's value.</param>
    /// <returns>The JSON object that maps each setting's name to its value.</returns>
    public static JsonElement Write(
        IEnumerable<Setting> settings,
        JsonSerializerOptions jsonOptions
    )
    {
        var values = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var setting in settings)
        {
            var value = setting.GetValue();
            values[setting.Descriptor.Name] = JsonSerializer.SerializeToElement(
                value,
                value.GetType(),
                jsonOptions
            );
        }

        // The names are written as they are, whatever naming policy the options carry.
        return JsonSerializer.SerializeToElement(values);
    }
}
