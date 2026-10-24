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
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Writes a server's effective settings as the JSON object that records what every tenant and client was reconciled
/// against: each setting's name and its value as the setting serializer reads it back, in ordinal order of the names.
/// </summary>
// See ADR-0234.
internal static class ServerSettingsSnapshot
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
