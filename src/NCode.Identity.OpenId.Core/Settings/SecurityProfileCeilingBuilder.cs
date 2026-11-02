// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the <see cref="ISecurityProfileCeilingBuilder"/> handed to one profile while its ceiling is built: it writes
/// each value through the setting's registered descriptor and rejects a setting that does not merge with a lattice
/// operation.
/// </summary>
/// <param name="profileName">The name of the profile whose ceiling is built, used to name it in an error.</param>
/// <param name="ceiling">The collection that receives the profile's ceiling settings.</param>
// See ADR-1148 (a profile's ceiling binds every scope below the one that selects it).
internal sealed class SecurityProfileCeilingBuilder(string profileName, ISettingCollection ceiling)
    : ISecurityProfileCeilingBuilder
{
    private string ProfileName { get; } = profileName;
    private ISettingCollection Ceiling { get; } = ceiling;

    /// <inheritdoc />
    public void Set<TValue>(SettingKey<TValue> key, TValue value)
        where TValue : notnull
    {
        // The collection resolves the registered descriptor and rejects a key without one or of another value type.
        Ceiling.Set(key, value);

        if (Ceiling.TryGet(key, out var setting) && setting.Descriptor.HasLatticeMerge)
            return;

        throw new InvalidOperationException(
            $"The security profile '{ProfileName}' sets a ceiling for '{key.SettingName}', which does not merge "
                + "with a lattice operation, so the ceiling would not bind every scope below the one that selects "
                + "the profile."
        );
    }
}
