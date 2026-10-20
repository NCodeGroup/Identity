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
using System.Text.Json;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation of <see cref="ISecurityProfileCatalog"/> over the registered
/// <see cref="ISecurityProfile"/> strategy collection. Profiles are keyed by their <see cref="ISecurityProfile.ProfileName"/>.
/// </summary>
/// <remarks>
/// <para>
/// A scope selects a set of profiles and every one applies: the ceiling settings of each profile a scope declares are
/// merged into that scope's settings, its default settings fill each choice the scope leaves unset, and the
/// requirements of each profile in the effective set are evaluated. The
/// profiles are visited in the ordinal order of their names, so the outcome never depends on how a list was written.
/// </para>
/// <para>
/// A selected name that matches no registered profile is an error, never "no profile": every entry point rejects such
/// a value, so one that still arrives here was set in code, and skipping it would silently drop the baseline the scope
/// selected.
/// </para>
/// </remarks>
// See ADR-0108 (profiles), ADR-0140 (the security_profiles domain), and ADR-0148 (profiles accumulate down the cascade).
internal sealed class DefaultSecurityProfileCatalog(
    IEnumerable<ISecurityProfile> profiles,
    ISettingSerializer settingSerializer,
    ISettingCollectionFactory settingCollectionFactory
) : ISecurityProfileCatalog
{
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private ISettingCollectionFactory SettingCollectionFactory { get; } = settingCollectionFactory;

    private FrozenDictionary<string, ISecurityProfile> ProfilesByName { get; } =
        profiles.ToFrozenDictionary(profile => profile.ProfileName, StringComparer.Ordinal);

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
        var selectedProfiles = GetSelectedProfiles(settings);
        if (selectedProfiles.Count == 0)
            return;

        // A requirement shared by several selected profiles is one rule, so it runs once.
        var evaluated = new HashSet<ISecurityProfileRequirement>(
            ReferenceEqualityComparer.Instance
        );

        foreach (var profile in selectedProfiles)
        {
            foreach (var requirement in profile.Requirements)
            {
                if (
                    requirement is ISecurityProfileRequirement<TContext> applicable
                    && evaluated.Add(requirement)
                )
                {
                    await applicable.EvaluateAsync(context, cancellationToken);
                }
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> Expand(IReadOnlyCollection<Setting> scopeSettings)
    {
        IReadOnlySettingCollection expanded = SettingCollectionFactory.Create(scopeSettings);
        var appliedProfileNames = new HashSet<string>(StringComparer.Ordinal);
        var appliedProfiles = new List<ISecurityProfile>();

        // A profile's ceiling may itself select the profiles it builds on, so expansion repeats until every profile
        // the scope carries has contributed its ceiling. It ends because each pass applies at least one registered
        // profile that no earlier pass applied.
        var pendingProfiles = GetSelectedProfiles(expanded);
        while (pendingProfiles.Count > 0)
        {
            foreach (var profile in pendingProfiles)
            {
                appliedProfileNames.Add(profile.ProfileName);
                appliedProfiles.Add(profile);
                expanded = ApplyCeiling(expanded, profile);
            }

            pendingProfiles = GetSelectedProfiles(expanded)
                .Where(profile => !appliedProfileNames.Contains(profile.ProfileName))
                .ToList();
        }

        return appliedProfiles.Count == 0
            ? scopeSettings
            : ApplyDefaults(expanded, appliedProfiles);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> Expand(
        JsonElement settingsJson,
        string scopeName,
        JsonSerializerOptions jsonOptions
    ) => Expand(SettingSerializer.DeserializeSettings(settingsJson, scopeName, jsonOptions));

    // Each profile's ceiling is built in its own collection and merged on its own: collecting several profiles into one
    // collection would let a later profile's value for a setting replace an earlier profile's instead of merging with it.
    private IReadOnlySettingCollection ApplyCeiling(
        IReadOnlySettingCollection settings,
        ISecurityProfile profile
    )
    {
        var ceiling = SettingCollectionFactory.Create();
        var builder = new SecurityProfileCeilingBuilder(profile.ProfileName, ceiling);
        profile.ApplyCeilingSettings(builder);
        return settings.Merge(ceiling);
    }

    // A profile's defaults sit beneath the scope's own settings: the scope's settings merge over them, so a value the
    // scope sets replaces the default. A default is never a lattice setting, so it cannot interact with a ceiling, and
    // when several profiles default one setting the profile whose name sorts first in ordinal order supplies it.
    private IReadOnlySettingCollection ApplyDefaults(
        IReadOnlySettingCollection settings,
        IEnumerable<ISecurityProfile> profiles
    )
    {
        var defaults = SettingCollectionFactory.Create();
        foreach (
            var profile in profiles.OrderBy(profile => profile.ProfileName, StringComparer.Ordinal)
        )
        {
            var profileDefaults = SettingCollectionFactory.Create();
            var builder = new SecurityProfileDefaultsBuilder(profile.ProfileName, profileDefaults);
            profile.ApplyDefaultSettings(builder);

            foreach (var setting in profileDefaults)
            {
                if (!defaults.TryGet(setting.Descriptor.Name, out _))
                    defaults.Set(setting);
            }
        }

        return defaults.Count == 0 ? settings : defaults.Merge(settings);
    }

    private List<ISecurityProfile> GetSelectedProfiles(IReadOnlySettingCollection settings)
    {
        if (
            !settings.TryGetValue(OpenIdSettingKeys.SecurityProfiles, out var profileNames)
            || profileNames.Count == 0
        )
        {
            return [];
        }

        var orderedNames = profileNames
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        var selectedProfiles = new List<ISecurityProfile>(profileNames.Count);
        foreach (var profileName in orderedNames)
        {
            if (!ProfilesByName.TryGetValue(profileName, out var profile))
            {
                throw new InvalidOperationException(
                    $"The security profile '{profileName}' is not registered, so the scope that selects it cannot be "
                        + "given the baseline it asked for."
                );
            }

            selectedProfiles.Add(profile);
        }

        return selectedProfiles;
    }
}
