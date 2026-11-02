// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the <see cref="ISecurityProfileDefaultsBuilder"/> handed to one profile while its defaults are built: it
/// writes each value through the setting's registered descriptor and rejects a setting that merges with a lattice
/// operation.
/// </summary>
/// <param name="profileName">The name of the profile whose defaults are built, used to name it in an error.</param>
/// <param name="defaults">The collection that receives the profile's default settings.</param>
// See ADR-1148 (a profile's defaults apply beneath the selecting scope's own values).
internal sealed class SecurityProfileDefaultsBuilder(
    string profileName,
    ISettingCollection defaults
) : ISecurityProfileDefaultsBuilder
{
    private string ProfileName { get; } = profileName;
    private ISettingCollection Defaults { get; } = defaults;

    /// <inheritdoc />
    public void Set<TValue>(SettingKey<TValue> key, TValue value)
        where TValue : notnull
    {
        // The collection resolves the registered descriptor and rejects a key without one or of another value type.
        Defaults.Set(key, value);

        if (Defaults.TryGet(key, out var setting) && !setting.Descriptor.HasLatticeMerge)
            return;

        throw new InvalidOperationException(
            $"The security profile '{ProfileName}' sets a default for '{key.SettingName}', which merges with a "
                + "lattice operation, so it bounds the scopes below rather than defaulting a choice; contribute it as "
                + "a ceiling instead."
        );
    }
}
