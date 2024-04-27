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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a strongly typed collection of <see cref="Setting"/> instances that can be accessed by name and value type.
/// </summary>
public interface ISettingCollection : IReadOnlySettingCollection
{
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
}
