// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
/// in the same way.
/// </summary>
// See ADR-1139 (closed vocabulary), ADR-1140 (value domains), and ADR-1149 (scopes).
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

        ReadSettings(settingsJson, scopeName, jsonOptions, settings, errors);

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

        ReadSettings(settingsJson, scopeName, jsonOptions, [], errors);

        return errors;
    }

    /// <inheritdoc />
    // See ADR-1418.
    public IReadOnlyList<string> GetChangedLimits(JsonElement currentJson, JsonElement newJson)
    {
        var current = ReadProperties(currentJson);
        var replacement = ReadProperties(newJson);
        var descriptors = SettingDescriptorCollectionProvider.Collection;

        var names = new HashSet<string>(current.Keys, StringComparer.Ordinal);
        names.UnionWith(replacement.Keys);

        var changed = new List<string>();
        foreach (var name in names)
        {
            if (!descriptors.TryGet(name, out var descriptor) || !descriptor.IsLimit)
                continue;

            var inCurrent = current.TryGetValue(name, out var currentValue);
            var inReplacement = replacement.TryGetValue(name, out var newValue);
            if (inCurrent != inReplacement || !JsonElement.DeepEquals(currentValue, newValue))
                changed.Add(name);
        }

        changed.Sort(StringComparer.Ordinal);
        return changed;
    }

    /// <inheritdoc />
    // See ADR-1418.
    public IReadOnlyList<string> GetLimits(string scopeName)
    {
        var scopes = SettingScopeCollectionProvider.Collection;
        if (!scopes.TryGet(scopeName, out _))
        {
            throw new ArgumentException(
                $"'{scopeName}' is not a registered setting scope.",
                nameof(scopeName)
            );
        }

        var limits = new List<string>();
        foreach (var descriptor in SettingDescriptorCollectionProvider.Collection)
        {
            if (descriptor.IsLimit && scopes.AppliesWithin(descriptor, scopeName))
                limits.Add(descriptor.Name);
        }

        limits.Sort(StringComparer.Ordinal);
        return limits;
    }

    private static Dictionary<string, JsonElement> ReadProperties(JsonElement settingsJson)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (settingsJson.ValueKind != JsonValueKind.Object)
            return properties;

        foreach (var jsonProperty in settingsJson.EnumerateObject())
            properties[jsonProperty.Name] = jsonProperty.Value;

        return properties;
    }

    private void ReadSettings(
        JsonElement settingsJson,
        string scopeName,
        JsonSerializerOptions jsonOptions,
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

            // A setting the scope never reads would be stored without effect, whatever its value (ADR-1149).
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

            // A value of the right type is still rejected when it is outside the setting's domain (ADR-1140).
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
