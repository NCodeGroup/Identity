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

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Collects the ceiling settings an <see cref="ISecurityProfile"/> contributes to the scope that selects it.
/// </summary>
/// <remarks>
/// A ceiling setting must be a registered setting that merges with a lattice operation
/// (<see cref="SettingDescriptor.HasLatticeMerge"/>): only then does the profile's value bound every scope below the
/// one that selects it, and do the ceilings of several selected profiles reach the same bound in any order. A setting
/// that a child scope could override, or whose merge would ignore the profile's value, is rejected, so a profile can
/// never be loosened down the cascade.
/// </remarks>
[PublicAPI]
public interface ISecurityProfileCeilingBuilder
{
    /// <summary>
    /// Sets the profile's ceiling value for the setting identified by <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key of the registered setting to constrain.</param>
    /// <param name="value">The ceiling value, which the setting's merge combines with the value of every scope.</param>
    /// <typeparam name="TValue">The value type of the setting.</typeparam>
    /// <exception cref="InvalidOperationException">The setting has no registered descriptor of
    /// <typeparamref name="TValue"/>, or it does not merge with a lattice operation.</exception>
    void Set<TValue>(SettingKey<TValue> key, TValue value)
        where TValue : notnull;
}
