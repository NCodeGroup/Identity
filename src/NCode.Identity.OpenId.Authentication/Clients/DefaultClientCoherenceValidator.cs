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
using System.Collections.Immutable;
using System.Text.Json;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="IClientCoherenceValidator"/> abstraction that checks the
/// settings of a client against every registered <see cref="IClientCoherenceRule"/>. A change is detected
/// generically from the <see cref="IClientCoherenceRule.SettingNames"/> of the rules: a setting that is present in only
/// one of the collections has changed, two lists of strings are compared as ordinal sets so that their order is
/// ignored, and any other pair of values is compared with <see cref="object.Equals(object?, object?)"/>.
/// </summary>
// See ADR-0206.
internal sealed class DefaultClientCoherenceValidator : IClientCoherenceValidator
{
    private ImmutableArray<IClientCoherenceRule> Rules { get; }

    private FrozenSet<string> SettingNames { get; }

    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; }

    private ISettingSerializer SettingSerializer { get; }

    private ISettingCollectionFactory SettingCollectionFactory { get; }

    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultClientCoherenceValidator"/> class with the registered rules.
    /// </summary>
    /// <param name="rules">The registered <see cref="IClientCoherenceRule"/> instances.</param>
    /// <param name="openIdEnvironmentProvider">The <see cref="IOpenIdEnvironmentProvider"/> that supplies the JSON
    /// serializer options.</param>
    /// <param name="settingSerializer">The <see cref="ISettingSerializer"/> that reads a client's settings document.</param>
    /// <param name="settingCollectionFactory">The <see cref="ISettingCollectionFactory"/> that creates the collection of
    /// registered settings.</param>
    /// <param name="effectiveSettingsResolver">The <see cref="IEffectiveSettingsResolver"/> that resolves the effective
    /// settings of a client.</param>
    public DefaultClientCoherenceValidator(
        IEnumerable<IClientCoherenceRule> rules,
        IOpenIdEnvironmentProvider openIdEnvironmentProvider,
        ISettingSerializer settingSerializer,
        ISettingCollectionFactory settingCollectionFactory,
        IEffectiveSettingsResolver effectiveSettingsResolver
    )
    {
        Rules = [.. rules];
        SettingNames = Rules.SelectMany(x => x.SettingNames).ToFrozenSet(StringComparer.Ordinal);
        OpenIdEnvironmentProvider = openIdEnvironmentProvider;
        SettingSerializer = settingSerializer;
        SettingCollectionFactory = settingCollectionFactory;
        EffectiveSettingsResolver = effectiveSettingsResolver;
    }

    /// <inheritdoc />
    public IReadOnlyList<ClientCoherenceViolation> Validate(
        IReadOnlySettingCollection tenantSettings,
        JsonElement clientSettings
    )
    {
        var context = new ClientCoherenceContext
        {
            RegisteredSettings = SettingCollectionFactory.Create(
                SettingSerializer.DeserializeSettings(
                    clientSettings,
                    OpenIdSettingScopes.Client,
                    OpenIdEnvironmentProvider.Get().JsonSerializerOptions
                )
            ),
            EffectiveSettings = EffectiveSettingsResolver.Resolve(
                tenantSettings,
                OpenIdSettingScopes.Client,
                clientSettings
            ),
        };

        return [.. Rules.SelectMany(rule => rule.FindViolations(context))];
    }

    /// <inheritdoc />
    public bool ReadsChangedSetting(
        IReadOnlySettingCollection previousSettings,
        IReadOnlySettingCollection currentSettings
    ) =>
        SettingNames.Any(settingName =>
            ValueChanged(previousSettings, currentSettings, settingName)
        );

    /// <summary>
    /// Determines whether the effective value of a setting differs between two collections, including a setting that is
    /// present in only one of them. Two lists of strings are compared as ordinal sets so that their order is ignored.
    /// </summary>
    /// <param name="previousSettings">The effective settings before the change.</param>
    /// <param name="currentSettings">The effective settings after the change.</param>
    /// <param name="settingName">The name of the setting to compare.</param>
    /// <returns><c>true</c> if the effective value of the setting differs; otherwise, <c>false</c>.</returns>
    private static bool ValueChanged(
        IReadOnlySettingCollection previousSettings,
        IReadOnlySettingCollection currentSettings,
        string settingName
    )
    {
        var hasPrevious = previousSettings.TryGetValue(settingName, out object? previous);
        var hasCurrent = currentSettings.TryGetValue(settingName, out object? current);

        if (!hasPrevious || !hasCurrent)
        {
            return hasPrevious != hasCurrent;
        }

        if (
            previous is IEnumerable<string> previousValues
            && current is IEnumerable<string> currentValues
        )
        {
            return !previousValues.ToHashSet(StringComparer.Ordinal).SetEquals(currentValues);
        }

        return !Equals(previous, current);
    }
}
