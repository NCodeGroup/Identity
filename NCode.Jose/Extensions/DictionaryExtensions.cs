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

namespace NCode.Jose.Extensions;

/// <summary>
/// Provides various extension methods for dictionary instances.
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// Attempts to retrieve a typed key-value pair from a dictionary.
    /// </summary>
    /// <param name="collection">The dictionary containing the key-value pairs.</param>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found;
    /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <returns><c>true</c> if a value with the specified key was found; otherwise, <c>false</c>.</returns>
    public static bool TryGetValue<T>(this IDictionary<string, object> collection, string key, [MaybeNullWhen(false)] out T value)
    {
        if (collection.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve a typed key-value pair from a dictionary.
    /// </summary>
    /// <param name="collection">The dictionary containing the key-value pairs.</param>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found;
    /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <returns><c>true</c> if a value with the specified key was found; otherwise, <c>false</c>.</returns>
    public static bool TryGetValue<T>(this IReadOnlyDictionary<string, object> collection, string key, [MaybeNullWhen(false)] out T value)
    {
        if (collection.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve a typed key-value pair from a dictionary.
    /// </summary>
    /// <param name="collection">The dictionary containing the key-value pairs.</param>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found;
    /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <returns><c>true</c> if a value with the specified key was found; otherwise, <c>false</c>.</returns>
    public static bool TryGetValue<T>(this Dictionary<string, object> collection, string key, [MaybeNullWhen(false)] out T value)
    {
        if (collection.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}
