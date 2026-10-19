#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a strongly typed read-only collection of <see cref="Setting"/> instances that can be accessed by name and value type.
/// </summary>
[PublicAPI]
public interface IReadOnlySettingCollection : IReadOnlyCollection<Setting>
{
    /// <summary>
    /// Gets the strongly typed setting with the specified name.
    /// If the setting hasn't been set, the default value from the descriptor is returned if available.
    /// Otherwise, the <see cref="KeyNotFoundException"/> is thrown.
    /// </summary>
    /// <param name="key">The key of the strongly typed setting to get.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns>The value of strongly typed setting with the specified name.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the setting with the specified name is not found.</exception>
    TValue GetValue<TValue>(SettingKey<TValue> key)
        where TValue : notnull;

    /// <summary>
    /// Attempts to get a strongly typed setting with the specified name.
    /// </summary>
    /// <param name="settingName">The name of the strongly typed setting to get.</param>
    /// <param name="setting">When this method returns, contains the strongly typed setting with the specified name,
    /// it the setting is found; otherwise, the default value for the type of the <paramref name="setting"/> parameter.
    /// This parameter is passed uninitialized.</param>
    /// <returns><c>true</c> if the collection contains a setting with the specified name; otherwise,
    /// <c>false</c>.</returns>
    bool TryGet(string settingName, [MaybeNullWhen(false)] out Setting setting);

    /// <summary>
    /// Attempts to get a strongly typed setting associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the strongly typed setting to get.</param>
    /// <param name="setting">When this method returns, contains the strongly typed setting with the specified key,
    /// it the setting is found; otherwise, the default value for the type of the <paramref name="setting"/> parameter.
    /// This parameter is passed uninitialized.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns><c>true</c> if the collection contains a setting with the specified key; otherwise, <c>false</c>.</returns>
    bool TryGet<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out Setting<TValue> setting)
        where TValue : notnull;

    /// <summary>
    /// Attempts to get a strongly typed setting value associated with the specified key.
    /// </summary>
    /// <param name="settingName">The name of the strongly typed setting to get.</param>
    /// <param name="value">When this method returns, contains the strongly typed setting value with the specified key,
    /// it the setting is found; otherwise, <c>null</c>.
    /// This parameter is passed uninitialized.</param>
    /// <returns><c>true</c> if the collection contains a setting with the specified key; otherwise, <c>false</c>.</returns>
    bool TryGetValue(string settingName, [MaybeNullWhen(false)] out object value);

    /// <summary>
    /// Attempts to get a strongly typed setting value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the strongly typed setting to get.</param>
    /// <param name="value">When this method returns, contains the strongly typed setting value with the specified key,
    /// it the setting is found; otherwise, the default value for <typeparamref name="TValue"/> type.
    /// This parameter is passed uninitialized.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns><c>true</c> if the collection contains a setting with the specified key; otherwise, <c>false</c>.</returns>
    bool TryGetValue<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out TValue value)
        where TValue : notnull;

    /// <summary>
    /// Creates and returns a new <see cref="ISettingCollection"/> instance that contains the settings from both the current
    /// instance and the <paramref name="otherCollection"/> by using the merge function defined by each setting's descriptor.
    /// </summary>
    /// <param name="otherCollection">The other collection to merge into the current collection.</param>
    /// <returns>The new <see cref="ISettingCollection"/> instance containing the merged settings from both collections.</returns>
    ISettingCollection Merge(IEnumerable<Setting> otherCollection);

    /// <summary>
    /// Creates and returns a new <see cref="ISettingCollection"/> instance bound by both the current instance and the
    /// <paramref name="otherCollection"/>: the settings of two sibling scopes that govern one operation together, rather
    /// than a parent and a child of the cascade (<see cref="Merge"/>).
    /// </summary>
    /// <remarks>
    /// A setting present in both collections is merged with its descriptor's merge function, which must be a lattice
    /// operation of <see cref="SettingMerge"/> (<see cref="SettingDescriptor.HasLatticeMerge"/>). Such a merge is
    /// commutative, associative, and idempotent, so the combined value binds both collections whatever their order and
    /// a value both inherit from a common ancestor is not counted twice. A setting present in only one collection keeps
    /// its value. A list keeps the order of the current instance, as <see cref="SettingMerge.Intersect{T}"/> keeps its
    /// left operand's.
    /// </remarks>
    /// <param name="otherCollection">The settings of the sibling scope to combine with the current collection.</param>
    /// <returns>The new <see cref="ISettingCollection"/> instance containing the combined settings.</returns>
    /// <exception cref="InvalidOperationException">A setting in <paramref name="otherCollection"/> does not merge with a
    /// lattice operation, so combining it would let the order of the collections decide its value.</exception>
    // See ADR-0154.
    ISettingCollection Combine(IEnumerable<Setting> otherCollection);
}
