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

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients.Coherence;

/// <summary>
/// Checks that a value the client's own document registers in a setting that merges with a lattice operation survives
/// into its effective settings: a list value or a <c>true</c> flag that its tenant or a selected security profile
/// excludes is reported rather than silently lost.
/// </summary>
// See ADR-0206.
internal sealed class RegisteredValueCoherenceRule(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
) : IClientCoherenceRule
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    /// <summary>
    /// Gets the names of every registered setting that merges with a lattice operation and whose value is a list of
    /// strings or a flag.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames =>
        [
            .. SettingDescriptorCollectionProvider
                .Collection.Where(descriptor =>
                    descriptor.HasLatticeMerge
                    && (
                        descriptor.ValueType == typeof(bool)
                        || typeof(IEnumerable<string>).IsAssignableFrom(descriptor.ValueType)
                    )
                )
                .Select(descriptor => descriptor.Name),
        ];

    /// <summary>
    /// Finds every list value and every <c>true</c> flag that a client registers in a setting that merges with a
    /// lattice operation and that its effective settings do not keep.
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
                !setting.Descriptor.HasLatticeMerge
                || !context.EffectiveSettings.TryGetValue(name, out var effective)
            )
            {
                continue;
            }

            switch (setting.GetValue())
            {
                case IEnumerable<string> values
                    when effective is IEnumerable<string> effectiveValues:
                    AddValuesThatAreNotEffective(violations, name, values, effectiveValues);
                    break;

                case true when effective is false:
                    violations.Add(new ClientCoherenceViolation(name, "true", name));
                    break;
            }
        }

        return violations;
    }

    /// <summary>
    /// Adds a violation for each distinct registered value that is not in the effective list.
    /// </summary>
    private static void AddValuesThatAreNotEffective(
        List<ClientCoherenceViolation> violations,
        string name,
        IEnumerable<string> values,
        IEnumerable<string> effectiveValues
    )
    {
        var effectiveSet = effectiveValues.ToHashSet(StringComparer.Ordinal);

        foreach (var value in values.Distinct(StringComparer.Ordinal))
        {
            if (!effectiveSet.Contains(value))
            {
                violations.Add(new ClientCoherenceViolation(name, value, name));
            }
        }
    }
}
