#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Identity.OpenId.Servers;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingSerializer"/> abstraction.
/// </summary>
public class DefaultSettingSerializer(
    OpenIdServer server
) : ISettingSerializer
{
    private OpenIdServer Server { get; } = server;

    /// <inheritdoc />
    public ISettingCollection DeserializeSettings(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson))
            return new SettingCollection();

        var settings = JsonSerializer.Deserialize<IEnumerable<Setting>>(
            settingsJson,
            Server.JsonSerializerOptions);

        return new SettingCollection(settings ?? Enumerable.Empty<Setting>());
    }

    /// <inheritdoc />
    public IReadOnlySettingCollection DeserializeSettings(IReadOnlySettingCollection parentSettings, string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson))
            return parentSettings;

        var settings = JsonSerializer.Deserialize<IEnumerable<Setting>>(
            settingsJson,
            Server.JsonSerializerOptions);

        if (settings == null)
            return parentSettings;

        return parentSettings.Merge(settings);
    }
}
