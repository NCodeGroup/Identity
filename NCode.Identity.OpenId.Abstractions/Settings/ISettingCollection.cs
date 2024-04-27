#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a strongly typed collection of <see cref="Setting"/> instances that can be accessed by name and value type.
/// </summary>
public interface ISettingCollection : IReadOnlyCollection<Setting>
{
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
    /// <returns><c>true</c> if the collection contains a setting with the specified key; otherwise,
    /// <c>false</c>.</returns>
    bool TryGet<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out Setting<TValue> setting)
        where TValue : notnull;

    /// <summary>
    /// Add or updates a strongly typed setting in the collection.
    /// </summary>
    /// <param name="setting">The strongly typed setting to set.</param>
    void Set(Setting setting);

    /// <summary>
    /// Removes a strongly typed setting with the specified key.
    /// </summary>
    /// <param name="key">The key of the strongly typed setting to remove.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns><c>true</c> if the setting is successfully found and removed; otherwise, <c>false</c>.
    /// The method returns <c>false</c> if <paramref name="key"/> is not found in the collection.</returns>
    bool Remove<TValue>(SettingKey<TValue> key)
        where TValue : notnull;

    /// <summary>
    /// Creates and returns a new <see cref="ISettingCollection"/> instance that contains the settings from both the current
    /// instance and the <paramref name="otherCollection"/> by using the merge function defined by each setting's descriptor.
    /// </summary>
    /// <param name="otherCollection">The other collection to merge into the current collection.</param>
    /// <returns>The new <see cref="ISettingCollection"/> instance containing the merged settings from both collections.</returns>
    ISettingCollection Merge(IEnumerable<Setting> otherCollection);
}
