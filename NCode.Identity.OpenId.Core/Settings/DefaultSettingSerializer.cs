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
    public IReadOnlyCollection<Setting> DeserializeSettings(JsonElement settingsJson)
        => settingsJson.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => Array.Empty<Setting>(),
            JsonValueKind.Object => settingsJson.Deserialize<IReadOnlyCollection<Setting>>(Server.JsonSerializerOptions) ?? [],
            _ => throw new JsonException("Expected an object or null value.")
        };
}
