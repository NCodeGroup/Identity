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

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingSerializer"/> abstraction.
/// </summary>
public class DefaultSettingSerializer(
    ISettingDescriptorJsonProvider settingDescriptorJsonProvider
) : ISettingSerializer
{
    private ISettingDescriptorJsonProvider SettingDescriptorJsonProvider { get; } = settingDescriptorJsonProvider;

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> DeserializeSettings(JsonElement settingsJson, JsonSerializerOptions jsonOptions)
    {
        if (settingsJson.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (settingsJson.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Expected an object or null value.");
        }

        var settings = new List<Setting>();

        foreach (var jsonProperty in settingsJson.EnumerateObject())
        {
            var descriptor = SettingDescriptorJsonProvider.GetDescriptor(jsonProperty.Name, jsonProperty.Value.ValueKind);
            var value = jsonProperty.Value.Deserialize(descriptor.ValueType, jsonOptions);

            Setting setting;
            if (value is not null)
            {
                setting = descriptor.Create(value);
            }
            else if (descriptor.HasDefault)
            {
                setting = descriptor.CreateDefault();
            }
            else
            {
                throw new JsonException($"Unable to deserialize value for {jsonProperty.Name}.");
            }

            settings.Add(setting);
        }

        return settings;
    }
}
