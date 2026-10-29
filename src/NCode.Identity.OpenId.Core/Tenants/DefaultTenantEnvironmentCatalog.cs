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

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the default implementation of <see cref="ITenantEnvironmentCatalog"/> over the registered
/// <see cref="ITenantEnvironmentType"/> strategy collection. Types are keyed by their
/// <see cref="ITenantEnvironmentType.TypeName"/>.
/// </summary>
/// <remarks>
/// The registered set is validated when the catalog is composed: a type with no name, a name registered more than once,
/// or a type that selects a security profile that is not registered is an error, never ignored. A scope that selects a
/// type that is not registered is an error too, never treated as if it had selected another type.
/// </remarks>
// See ADR-0289.
internal sealed class DefaultTenantEnvironmentCatalog(
    IEnumerable<ITenantEnvironmentType> types,
    ISecurityProfileCatalog securityProfileCatalog,
    ISettingCollectionFactory settingCollectionFactory,
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
) : ITenantEnvironmentCatalog
{
    private ISettingCollectionFactory SettingCollectionFactory { get; } = settingCollectionFactory;
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    private FrozenDictionary<string, ITenantEnvironmentType> TypesByName { get; } =
        Index(types, securityProfileCatalog);

    /// <inheritdoc />
    public bool TryGetType(string typeName, [MaybeNullWhen(false)] out ITenantEnvironmentType type)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            type = null;
            return false;
        }

        return TypesByName.TryGetValue(typeName, out type);
    }

    /// <inheritdoc />
    public ITenantEnvironmentType GetEffectiveType(IReadOnlySettingCollection settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return GetRequiredType(settings.GetValue(OpenIdSettingKeys.EnvironmentType));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> SelectImpliedProfiles(
        IReadOnlySettingCollection parentSettings,
        string scopeName,
        IReadOnlyCollection<Setting> scopeSettings
    )
    {
        ArgumentNullException.ThrowIfNull(parentSettings);
        ArgumentNullException.ThrowIfNull(scopeName);
        ArgumentNullException.ThrowIfNull(scopeSettings);

        if (
            !SettingDescriptorCollectionProvider.Collection.TryGet(
                OpenIdSettingKeys.EnvironmentType,
                out var descriptor
            ) || !descriptor.AppliesTo.Contains(scopeName)
        )
        {
            return scopeSettings;
        }

        var collection = SettingCollectionFactory.Create(scopeSettings);

        var typeName = collection.TryGetValue(
            OpenIdSettingKeys.EnvironmentType,
            out var ownTypeName
        )
            ? ownTypeName
            : parentSettings.GetValue(OpenIdSettingKeys.EnvironmentType);

        var type = GetRequiredType(typeName);
        if (type.SecurityProfiles.Count == 0)
            return scopeSettings;

        var selectedProfiles = collection.TryGetValue(
            OpenIdSettingKeys.SecurityProfiles,
            out var existingProfiles
        )
            ? existingProfiles
            : [];

        IReadOnlyCollection<string> impliedProfiles = selectedProfiles
            .Union(type.SecurityProfiles, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        collection.Set(OpenIdSettingKeys.SecurityProfiles, impliedProfiles);
        return collection;
    }

    private ITenantEnvironmentType GetRequiredType(string typeName)
    {
        if (!TryGetType(typeName, out var type))
        {
            throw new InvalidOperationException(
                $"The tenant environment type '{typeName}' is not registered."
            );
        }

        return type;
    }

    private static FrozenDictionary<string, ITenantEnvironmentType> Index(
        IEnumerable<ITenantEnvironmentType> types,
        ISecurityProfileCatalog securityProfileCatalog
    )
    {
        var typesByName = new Dictionary<string, ITenantEnvironmentType>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            var typeName = type.TypeName;
            if (string.IsNullOrEmpty(typeName))
                throw new InvalidOperationException("A tenant environment type has no name.");

            foreach (var profileName in type.SecurityProfiles)
            {
                if (!securityProfileCatalog.TryGetProfile(profileName, out _))
                {
                    throw new InvalidOperationException(
                        $"The tenant environment type '{typeName}' selects the security profile '{profileName}', "
                            + "which is not registered."
                    );
                }
            }

            if (!typesByName.TryAdd(typeName, type))
            {
                throw new InvalidOperationException(
                    $"The tenant environment type '{typeName}' is registered more than once."
                );
            }
        }

        return typesByName.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
