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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Json;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingSerializer"/> abstraction. Each setting is typed by its
/// registered <see cref="SettingDescriptor"/>; a setting without one is an error rather than an ad-hoc value, so a
/// misspelled or unregistered setting name can never be stored or loaded without being honored. A setting must also
/// apply within the registered scope whose document carries it
/// (<see cref="ISettingScopeCollection.AppliesWithin(SettingDescriptor, string, out string?)"/>), and a value of the
/// right type must be in the setting's domain (<see cref="SettingDescriptor.TryValidate"/>), so a setting the scope
/// never reads, or a value the server would not honor — an unknown security profile, a negative lifetime — is rejected
/// in the same way. A retired setting (<see cref="SettingDescriptor.IsRetired"/>) is ignored when a stored document is
/// read and refused when a document is validated.
/// </summary>
// See ADR-0139 (closed vocabulary), ADR-0140 (value domains), ADR-0149 (scopes), and ADR-0251.
internal class DefaultSettingSerializer(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
    ISettingScopeCollectionProvider settingScopeCollectionProvider
) : ISettingSerializer
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> DeserializeSettings(
        JsonElement settingsJson,
        string scopeName,
        JsonSerializerOptions jsonOptions
    )
    {
        var settings = new List<Setting>();
        var errors = new List<SettingError>();

        ReadSettings(settingsJson, scopeName, jsonOptions, static (_, _) => true, settings, errors);

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
        string scopeName,
        JsonSerializerOptions jsonOptions
    )
    {
        var errors = new List<SettingError>();

        ReadSettings(settingsJson, scopeName, jsonOptions, static (_, _) => false, [], errors);

        return errors;
    }

    /// <inheritdoc />
    public IReadOnlyList<SettingError> ValidateSettingsUpdate(
        JsonElement storedSettingsJson,
        JsonElement settingsJson,
        string scopeName,
        JsonSerializerOptions jsonOptions
    )
    {
        var errors = new List<SettingError>();

        ReadSettings(
            settingsJson,
            scopeName,
            jsonOptions,
            (name, value) => KeepsStoredValue(storedSettingsJson, name, value),
            [],
            errors
        );

        return errors;
    }

    private static bool KeepsStoredValue(
        JsonElement stored,
        string settingName,
        JsonElement value
    ) =>
        stored.ValueKind == JsonValueKind.Object
        && stored.TryGetProperty(settingName, out var storedValue)
        && JsonElement.DeepEquals(storedValue, value);

    private void ReadSettings(
        JsonElement settingsJson,
        string scopeName,
        JsonSerializerOptions jsonOptions,
        Func<string, JsonElement, bool> acceptsRetiredSetting,
        List<Setting> settings,
        List<SettingError> errors
    )
    {
        var scopes = SettingScopeCollectionProvider.Collection;
        if (!scopes.TryGet(scopeName, out _))
        {
            throw new ArgumentException(
                $"'{scopeName}' is not a registered setting scope.",
                nameof(scopeName)
            );
        }

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

            // A retired setting is never read; a validation accepts it only where the caller allows (ADR-0251).
            if (descriptor.IsRetired)
            {
                if (!acceptsRetiredSetting(settingName, jsonProperty.Value))
                {
                    errors.Add(
                        new SettingError(
                            settingName,
                            $"The setting '{settingName}' is retired. {descriptor.RetiredReason}"
                        )
                    );
                }

                continue;
            }

            // A setting the scope never reads would be stored without effect, whatever its value (ADR-0149).
            if (!scopes.AppliesWithin(descriptor, scopeName, out var scopeFailure))
            {
                errors.Add(
                    new SettingError(settingName, $"The setting '{settingName}' {scopeFailure}.")
                );
                continue;
            }

            if (!TryCreateSetting(descriptor, jsonProperty.Value, jsonOptions, out var setting))
            {
                var expected = DescribeValueType(descriptor.ValueType);
                var message =
                    jsonProperty.Value.ValueKind == JsonValueKind.Null
                        ? $"The setting '{settingName}' has no default, so it must be {expected}, not null."
                        : $"The setting '{settingName}' must be {expected}.";
                errors.Add(new SettingError(settingName, message));
                continue;
            }

            // A value of the right type is still rejected when it is outside the setting's domain (ADR-0140).
            if (!descriptor.TryValidate(setting.GetValue(), out var failure))
            {
                errors.Add(
                    new SettingError(settingName, $"The setting '{settingName}' {failure}.")
                );
                continue;
            }

            settings.Add(setting);
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
            // A JSON array can carry a null that the declared non-nullable element type does not admit.
            if (ContainsNull(value))
            {
                setting = null;
                return false;
            }

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

    private static bool ContainsNull(object value) =>
        value is IEnumerable enumerable and not string
        && enumerable.Cast<object?>().Any(item => item is null);

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
