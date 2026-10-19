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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the <see cref="ISecurityProfileCeilingBuilder"/> handed to one profile while its ceiling is built: it writes
/// each value through the setting's registered descriptor and rejects a setting that does not merge with a lattice
/// operation.
/// </summary>
/// <param name="profileName">The name of the profile whose ceiling is built, used to name it in an error.</param>
/// <param name="ceiling">The collection that receives the profile's ceiling settings.</param>
// See ADR-0148 (a profile's ceiling binds every scope below the one that selects it).
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
