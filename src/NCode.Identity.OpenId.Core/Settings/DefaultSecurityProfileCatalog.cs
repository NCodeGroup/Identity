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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation of <see cref="ISecurityProfileCatalog"/> over the registered
/// <see cref="ISecurityProfile"/> strategy collection. Profiles are keyed by their <see cref="ISecurityProfile.ProfileName"/>.
/// </summary>
internal sealed class DefaultSecurityProfileCatalog : ISecurityProfileCatalog
{
    private ISettingSerializer SettingSerializer { get; }
    private ISettingCollectionFactory SettingCollectionFactory { get; }
    private Dictionary<string, ISecurityProfile> ProfilesByName { get; }

    public DefaultSecurityProfileCatalog(
        IEnumerable<ISecurityProfile> profiles,
        ISettingSerializer settingSerializer,
        ISettingCollectionFactory settingCollectionFactory
    )
    {
        SettingSerializer = settingSerializer;
        SettingCollectionFactory = settingCollectionFactory;
        ProfilesByName = profiles.ToDictionary(
            profile => profile.ProfileName,
            StringComparer.Ordinal
        );
    }

    /// <inheritdoc />
    public bool TryGetProfile(
        string profileName,
        [MaybeNullWhen(false)] out ISecurityProfile profile
    )
    {
        if (string.IsNullOrEmpty(profileName))
        {
            profile = null;
            return false;
        }

        return ProfilesByName.TryGetValue(profileName, out profile);
    }

    /// <inheritdoc />
    public async ValueTask EvaluateRequirementsAsync<TContext>(
        IReadOnlySettingCollection settings,
        TContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            !settings.TryGetValue(OpenIdSettingKeys.SecurityProfile, out var profileName)
            || !TryGetProfile(profileName, out var profile)
        )
        {
            return;
        }

        foreach (var requirement in profile.Requirements)
        {
            if (requirement is ISecurityProfileRequirement<TContext> applicable)
                await applicable.EvaluateAsync(context, cancellationToken);
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> Expand(IReadOnlyCollection<Setting> scopeSettings)
    {
        var collection = SettingCollectionFactory.Create(scopeSettings);

        if (
            !collection.TryGetValue(OpenIdSettingKeys.SecurityProfile, out var profileName)
            || !TryGetProfile(profileName, out var profile)
        )
        {
            return scopeSettings;
        }

        var ceiling = SettingCollectionFactory.Create();
        profile.ApplyCeilingSettings(ceiling);

        return collection.Merge(ceiling);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> Expand(
        JsonElement settingsJson,
        JsonSerializerOptions jsonOptions
    ) => Expand(SettingSerializer.DeserializeSettings(settingsJson, jsonOptions));
}
