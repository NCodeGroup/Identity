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

using System.Collections;
using System.Globalization;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients.Coherence;

/// <summary>
/// Checks that a value the client's own document registers in a setting that merges with a meet survives into its
/// effective settings: a list value a ceiling drops, a <c>true</c> a ceiling turns off, and any other value a ceiling
/// lowers (a lifetime above its tenant's) is reported rather than silently lost. A join only raises a value to a
/// parent's floor and is never reported.
/// </summary>
// See ADR-0206 and ADR-0240.
internal sealed class RegisteredValueCoherenceRule(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
    ISettingScopeCollectionProvider settingScopeCollectionProvider
) : IClientCoherenceRule
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;

    /// <summary>
    /// Gets the names of every registered setting that merges with a meet and applies within the client scope, so a
    /// value a client registers in it can be lowered by a ceiling.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames
    {
        get
        {
            var scopes = SettingScopeCollectionProvider.Collection;

            return
            [
                .. SettingDescriptorCollectionProvider
                    .Collection.Where(descriptor =>
                        descriptor.HasMeetMerge
                        && scopes.AppliesWithin(descriptor, OpenIdSettingScopes.Client)
                    )
                    .Select(descriptor => descriptor.Name),
            ];
        }
    }

    /// <summary>
    /// Finds every value that a client registers in a setting that merges with a meet and that its effective settings
    /// do not keep: a list element, a <c>true</c> flag, or any other value a ceiling lowers.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violations, or an empty list when every registered value is effective.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        List<ClientCoherenceViolation> violations = [];

        foreach (var setting in context.RegisteredSettings)
        {
            var name = setting.Descriptor.Name;

            if (
                !setting.Descriptor.HasMeetMerge
                || !context.EffectiveSettings.TryGetValue(name, out var effective)
            )
            {
                continue;
            }

            switch (setting.GetValue())
            {
                case bool registered:
                    if (registered && effective is false)
                    {
                        violations.Add(new ClientCoherenceViolation(name, "true", name));
                    }

                    break;

                case IEnumerable values when values is not string:
                    if (effective is IEnumerable effectiveValues)
                    {
                        AddValuesThatAreNotEffective(violations, name, values, effectiveValues);
                    }

                    break;

                case var value when !Equals(value, effective):
                    violations.Add(CreateLoweredValueViolation(name, value, effective));
                    break;
            }
        }

        return violations;
    }

    /// <summary>
    /// Adds a violation for each distinct registered element that is not in the effective elements.
    /// </summary>
    private static void AddValuesThatAreNotEffective(
        List<ClientCoherenceViolation> violations,
        string name,
        IEnumerable values,
        IEnumerable effectiveValues
    )
    {
        var effectiveSet = effectiveValues.Cast<object>().ToHashSet();

        foreach (var value in values.Cast<object>().Distinct())
        {
            if (!effectiveSet.Contains(value))
            {
                violations.Add(new ClientCoherenceViolation(name, FormatValue(value), name));
            }
        }
    }

    /// <summary>
    /// Creates the violation for a registered value that a ceiling lowers to the effective value.
    /// </summary>
    private static ClientCoherenceViolation CreateLoweredValueViolation(
        string name,
        object registered,
        object effective
    )
    {
        var registeredText = FormatValue(registered);
        var effectiveText = FormatValue(effective);

        return new ClientCoherenceViolation(name, registeredText, name)
        {
            Description =
                $"The '{name}' value '{registeredText}' exceeds the effective '{name}' value '{effectiveText}'.",
        };
    }

    /// <summary>
    /// Formats a value for a violation without depending on the current culture.
    /// </summary>
    private static string FormatValue(object value) =>
        value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString() ?? string.Empty;
}
