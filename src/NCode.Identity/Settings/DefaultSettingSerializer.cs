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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Json;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingSerializer"/> abstraction. Each setting is typed by its
/// registered <see cref="SettingDescriptor"/>; a setting without one is an error rather than an ad-hoc value, so a
/// misspelled or retired setting name can never be stored or loaded without being honored.
/// </summary>
// See ADR-0139.
internal class DefaultSettingSerializer(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
) : ISettingSerializer
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> DeserializeSettings(
        JsonElement settingsJson,
        JsonSerializerOptions jsonOptions
    )
    {
        var settings = new List<Setting>();
        var errors = new List<SettingError>();

        ReadSettings(settingsJson, jsonOptions, settings, errors);

        if (errors.Count > 0)
        {
            var messages = errors.Select(error => error.Message);
            var details = string.Join(" ", messages);
            throw new JsonException($"The settings are invalid: {details}");
        }

        return settings;
    }

    /// <inheritdoc />
    public IReadOnlyList<SettingError> ValidateSettings(
        JsonElement settingsJson,
        JsonSerializerOptions jsonOptions
    )
    {
        var errors = new List<SettingError>();

        ReadSettings(settingsJson, jsonOptions, [], errors);

        return errors;
    }

    private void ReadSettings(
        JsonElement settingsJson,
        JsonSerializerOptions jsonOptions,
        List<Setting> settings,
        List<SettingError> errors
    )
    {
        if (settingsJson.IsNullOrUndefined())
            return;

        if (settingsJson.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new SettingError(string.Empty, "The settings must be a JSON object."));
            return;
        }

        var descriptors = SettingDescriptorCollectionProvider.Collection;
        foreach (var jsonProperty in settingsJson.EnumerateObject())
        {
            var settingName = jsonProperty.Name;
            if (!descriptors.TryGet(settingName, out var descriptor))
            {
                errors.Add(
                    new SettingError(settingName, $"'{settingName}' is not a registered setting.")
                );
                continue;
            }

            if (TryCreateSetting(descriptor, jsonProperty.Value, jsonOptions, out var setting))
            {
                settings.Add(setting);
                continue;
            }

            var expected = DescribeValueType(descriptor.ValueType);
            var message =
                jsonProperty.Value.ValueKind == JsonValueKind.Null
                    ? $"The setting '{settingName}' has no default, so it must be {expected}, not null."
                    : $"The setting '{settingName}' must be {expected}.";
            errors.Add(new SettingError(settingName, message));
        }
    }

    private static bool TryCreateSetting(
        SettingDescriptor descriptor,
        JsonElement valueJson,
        JsonSerializerOptions jsonOptions,
        [NotNullWhen(true)] out Setting? setting
    )
    {
        object? value;
        try
        {
            value = valueJson.Deserialize(descriptor.ValueType, jsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            setting = null;
            return false;
        }

        if (value is not null)
        {
            setting = descriptor.Create(value);
            return true;
        }

        if (descriptor.HasDefault)
        {
            setting = descriptor.CreateDefault();
            return true;
        }

        setting = null;
        return false;
    }

    private static string DescribeValueType(Type valueType)
    {
        if (valueType == typeof(string))
            return "a string";
        if (valueType == typeof(bool))
            return "true or false";
        if (valueType == typeof(int) || valueType == typeof(long))
            return "an integer";
        if (valueType == typeof(double) || valueType == typeof(decimal))
            return "a number";
        if (valueType == typeof(TimeSpan))
            return "a time span such as \"00:05:00\"";
        if (valueType == typeof(JsonElement))
            return "a JSON value";
        if (typeof(IEnumerable<string>).IsAssignableFrom(valueType))
            return "an array of strings";
        return $"a {valueType.Name} value";
    }
}
