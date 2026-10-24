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

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation of <see cref="ISettingConfigurationReader"/>. A list-valued setting is read from
/// indexed entries (an empty list from an empty array); a single-valued setting is converted from its value by the
/// configuration binder. A setting that does not apply within the scope, or a value whose shape or type does not match
/// the descriptor or that is outside the setting's domain, is reported, never skipped. A retired setting is reported,
/// never read.
/// </summary>
// See ADR-0139 (closed vocabulary), ADR-0140 (value domains), and ADR-0149 (scopes).
internal class DefaultSettingConfigurationReader(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
    ISettingScopeCollectionProvider settingScopeCollectionProvider
) : ISettingConfigurationReader
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(IConfigurationSection section, string scopeName)
    {
        var errors = new List<string>();

        ReadSettings(section, scopeName, [], errors);

        return errors;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> Read(IConfigurationSection section, string scopeName)
    {
        var settings = new List<Setting>();
        var errors = new List<string>();

        ReadSettings(section, scopeName, settings, errors);

        if (errors.Count > 0)
        {
            var details = string.Join(" ", errors);
            throw new InvalidOperationException(
                $"The settings in the configuration section '{section.Path}' cannot be honored: {details}"
            );
        }

        return settings;
    }

    private void ReadSettings(
        IConfigurationSection section,
        string scopeName,
        List<Setting> settings,
        List<string> errors
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

        var descriptors = SettingDescriptorCollectionProvider.Collection;
        foreach (var child in section.GetChildren())
        {
            if (!descriptors.TryGet(child.Key, out var descriptor))
            {
                errors.Add($"'{child.Path}' is not a registered setting.");
                continue;
            }

            if (descriptor.IsRetired)
            {
                errors.Add($"'{child.Path}' is retired. {descriptor.RetiredReason}");
                continue;
            }

            if (!scopes.AppliesWithin(descriptor, scopeName, out var scopeFailure))
            {
                errors.Add($"'{child.Path}' {scopeFailure}.");
                continue;
            }

            if (!TryReadValue(child, descriptor.ValueType, out var value, out var error))
            {
                errors.Add(error);
                continue;
            }

            if (!descriptor.TryValidate(value, out var failure))
            {
                errors.Add($"'{child.Path}' {failure}.");
                continue;
            }

            settings.Add(descriptor.Create(value));
        }
    }

    private static bool TryReadValue(
        IConfigurationSection child,
        Type valueType,
        [NotNullWhen(true)] out object? value,
        [NotNullWhen(false)] out string? error
    )
    {
        var hasEntries = child.GetChildren().Any();

        if (TryGetElementType(valueType, out var elementType))
        {
            if (!string.IsNullOrEmpty(child.Value))
            {
                value = null;
                error =
                    $"'{child.Path}' must be a list of values, one indexed entry per value, not a single value.";
                return false;
            }

            if (!hasEntries)
            {
                return TryCreateEmptyCollection(
                    child,
                    valueType,
                    elementType,
                    out value,
                    out error
                );
            }

            return TryBind(child, valueType, out value, out error);
        }

        if ((child.Value is null || hasEntries) && IsSingleValue(valueType))
        {
            value = null;
            error = $"'{child.Path}' must be a single value, not a list or a section.";
            return false;
        }

        return TryBind(child, valueType, out value, out error);
    }

    private static bool TryBind(
        IConfigurationSection child,
        Type valueType,
        [NotNullWhen(true)] out object? value,
        [NotNullWhen(false)] out string? error
    )
    {
        try
        {
            value = child.Get(
                valueType,
                binderOptions => binderOptions.ErrorOnUnknownConfiguration = true
            );
        }
        catch (InvalidOperationException exception)
        {
            value = null;
            error = exception.Message;
            return false;
        }

        if (value is null)
        {
            error = $"'{child.Path}' has no value.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryCreateEmptyCollection(
        IConfigurationSection child,
        Type valueType,
        Type elementType,
        [NotNullWhen(true)] out object? value,
        [NotNullWhen(false)] out string? error
    )
    {
        var emptyArray = Array.CreateInstance(elementType, 0);
        if (valueType.IsInstanceOfType(emptyArray))
        {
            value = emptyArray;
            error = null;
            return true;
        }

        if (!valueType.IsAbstract && valueType.GetConstructor(Type.EmptyTypes) is not null)
        {
            value = Activator.CreateInstance(valueType);
            if (value is not null)
            {
                error = null;
                return true;
            }
        }

        value = null;
        error = $"'{child.Path}' cannot be an empty list.";
        return false;
    }

    private static bool IsSingleValue(Type valueType) =>
        TypeDescriptor.GetConverter(valueType).CanConvertFrom(typeof(string));

    private static bool TryGetElementType(Type valueType, [NotNullWhen(true)] out Type? elementType)
    {
        if (valueType == typeof(string))
        {
            elementType = null;
            return false;
        }

        var enumerableType = IsEnumerableOfT(valueType)
            ? valueType
            : valueType.GetInterfaces().FirstOrDefault(IsEnumerableOfT);

        elementType = enumerableType?.GetGenericArguments()[0];
        return elementType is not null;
    }

    private static bool IsEnumerableOfT(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>);
}
