#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides extensions methods for the <see cref="IReadOnlySettingCollection"/> abstraction.
/// </summary>
[PublicAPI]
public static class ReadOnlySettingCollectionExtensions
{
    /// <summary>
    /// Attempts to get a strongly typed setting value associated with the specified key.
    /// </summary>
    /// <param name="collection">The collection to search for the setting.</param>
    /// <param name="key">The key of the strongly typed setting to get.</param>
    /// <param name="value">When this method returns, contains the strongly typed setting value with the specified key,
    /// it the setting is found; otherwise, the default value for the <typeparamref name="TValue"/> type.
    /// This parameter is passed uninitialized.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns><c>true</c> if the collection contains a setting with the specified key; otherwise, <c>false</c>.</returns>
    public static bool TryGetValue<TValue>(this IReadOnlySettingCollection collection, SettingKey<TValue> key, [MaybeNullWhen(false)] out TValue value)
        where TValue : notnull
    {
        if (collection.TryGet(key, out var setting))
        {
            value = setting.Value;
            return true;
        }

        value = default;
        return false;
    }
}
