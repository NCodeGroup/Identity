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
/// Collects the default settings an <see cref="ISecurityProfile"/> contributes to the scope that selects it.
/// </summary>
/// <remarks>
/// A default setting must be a registered setting that does not merge with a lattice operation
/// (<see cref="SettingDescriptor.HasLatticeMerge"/>): it is a value the scope chooses, such as the algorithm a client
/// signs its ID tokens with, so the scope that selects the profile and every scope below it may still set another value.
/// A lattice setting bounds the scopes below and is contributed as a ceiling instead
/// (<see cref="ISecurityProfileCeilingBuilder"/>), so it is rejected here.
/// </remarks>
[PublicAPI]
public interface ISecurityProfileDefaultsBuilder
{
    /// <summary>
    /// Sets the profile's default value for the setting identified by <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key of the registered setting to default.</param>
    /// <param name="value">The default value, which applies unless the selecting scope or a scope below it sets
    /// another.</param>
    /// <typeparam name="TValue">The value type of the setting.</typeparam>
    /// <exception cref="InvalidOperationException">The setting has no registered descriptor of
    /// <typeparamref name="TValue"/>, or it merges with a lattice operation.</exception>
    void Set<TValue>(SettingKey<TValue> key, TValue value)
        where TValue : notnull;
}
