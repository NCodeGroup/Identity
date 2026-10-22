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
/// merged into that scope's settings, its default settings fill each choice the scope leaves unset when the setting
/// applies within the scope (to the scope itself or to a scope below it), and the requirements of each profile in
/// the effective set are evaluated. The profiles are visited in the ordinal order of their names, so the outcome
/// never depends on how a list was written.
/// </para>
/// <para>
/// A selected name that matches no registered profile is an error, never "no profile": every entry point rejects such
/// a value, so one that still arrives here was set in code, and skipping it would silently drop the baseline the scope
/// selected.
/// </para>
/// </remarks>
// See ADR-0108 (profiles), ADR-0140 (the security_profiles domain), ADR-0148 (profiles accumulate down the cascade),
// and ADR-0149 (the scope a setting applies within).
internal sealed class DefaultSecurityProfileCatalog(
    IEnumerable<ISecurityProfile> profiles,
    ISettingSerializer settingSerializer,
    ISettingCollectionFactory settingCollectionFactory,
    ISettingScopeCollectionProvider settingScopeCollectionProvider
) : ISecurityProfileCatalog
{
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private ISettingCollectionFactory SettingCollectionFactory { get; } = settingCollectionFactory;
    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;

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
    public IReadOnlyCollection<Setting> Expand(
        IReadOnlyCollection<Setting> scopeSettings,
        string scopeName
    ) => Expand(scopeSettings, scopeName, []);

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> Expand(
        IReadOnlyCollection<Setting> scopeSettings,
        string scopeName,
        IReadOnlyCollection<Setting> baselineSettings
    )
    {
        ArgumentNullException.ThrowIfNull(scopeName);

        var scopes = SettingScopeCollectionProvider.Collection;
        if (!scopes.TryGet(scopeName, out _))
        {
            throw new ArgumentException(
                $"'{scopeName}' is not a registered setting scope.",
                nameof(scopeName)
            );
        }

        IReadOnlySettingCollection expanded = Layer(baselineSettings, scopeSettings);
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

        if (appliedProfiles.Count == 0)
            return baselineSettings.Count == 0 ? scopeSettings : expanded;

        return ApplyDefaults(expanded, appliedProfiles, scopeSettings, scopes, scopeName);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> Expand(
        JsonElement settingsJson,
        string scopeName,
        JsonSerializerOptions jsonOptions
    ) =>
        Expand(
            SettingSerializer.DeserializeSettings(settingsJson, scopeName, jsonOptions),
            scopeName
        );

    // The scope's own settings replace the baseline's, as the server's configuration replaces a descriptor default.
    private IReadOnlySettingCollection Layer(
        IReadOnlyCollection<Setting> baselineSettings,
        IReadOnlyCollection<Setting> scopeSettings
    )
    {
        if (baselineSettings.Count == 0)
            return SettingCollectionFactory.Create(scopeSettings);

        var layered = SettingCollectionFactory.Create(baselineSettings);
        foreach (var setting in scopeSettings)
        {
            layered.Set(setting);
        }

        return layered;
    }

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

    // A profile's defaults sit beneath the scope's own settings and above its baseline: a value the scope sets
    // replaces the default, and the default replaces a baseline value. A default is never a lattice setting, so it
    // cannot interact with a ceiling, and when several profiles default one setting the profile whose name sorts
    // first in ordinal order supplies it. A default applies only to a setting that applies within the expanding scope
    // (the scope itself or a scope below it): a setting for a scope above it would be carried without being honored,
    // and the scope that does own it chooses its own default. A ceiling is not filtered the same way, because a
    // bound below the scope a setting applies to only narrows.
    private IReadOnlyCollection<Setting> ApplyDefaults(
        IReadOnlySettingCollection settings,
        IEnumerable<ISecurityProfile> profiles,
        IReadOnlyCollection<Setting> scopeSettings,
        ISettingScopeCollection scopes,
        string scopeName
    )
    {
        var scopeSettingNames = scopeSettings
            .Select(setting => setting.Descriptor.Name)
            .ToHashSet(StringComparer.Ordinal);
        var defaults = new Dictionary<string, Setting>(StringComparer.Ordinal);
        foreach (
            var profile in profiles.OrderBy(profile => profile.ProfileName, StringComparer.Ordinal)
        )
        {
            var profileDefaults = SettingCollectionFactory.Create();
            var builder = new SecurityProfileDefaultsBuilder(profile.ProfileName, profileDefaults);
            profile.ApplyDefaultSettings(builder);

            foreach (var setting in profileDefaults)
            {
                var name = setting.Descriptor.Name;
                if (
                    !scopeSettingNames.Contains(name)
                    && scopes.AppliesWithin(setting.Descriptor, scopeName)
                )
                {
                    defaults.TryAdd(name, setting);
                }
            }
        }

        if (defaults.Count == 0)
            return settings;

        var result = SettingCollectionFactory.Create(settings);
        foreach (var setting in defaults.Values)
        {
            result.Set(setting);
        }

        return result;
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
