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

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Contains the unparsed values for an OpenID Connect request.
/// </summary>
[PublicAPI]
public interface IOpenIdRequestValues : IEnumerable<KeyValuePair<string, StringValues>>
{
    /// <summary>
    /// Gets a string that identifies the source of the request values.
    /// </summary>
    string SourceType { get; }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="IOpenIdRequestValues"/>.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets an <see cref="ICollection{T}"/> containing the keys of the <see cref="IOpenIdRequestValues"/>.
    /// </summary>
    ICollection<string> Keys { get; }

    /// <summary>
    /// Determines whether the <see cref="IOpenIdRequestValues"/> contains an element with the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the <see cref="IOpenIdRequestValues"/>.</param>
    /// <returns><c>true</c> if the <see cref="IOpenIdRequestValues"/> contains an element with the specified key; otherwise, <c>false</c>.</returns>
    bool ContainsKey(string key);

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns><c>true</c> if the <see cref="IOpenIdRequestValues"/> contains an element with the specified key; otherwise, <c>false</c>.</returns>
    bool TryGetValue(string key, out StringValues value);

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>The element with the specified key, or <c>StringValues.Empty</c> if the key is not present.</returns>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="key"/> is <c>null</c>.</exception>
    StringValues this[string key] { get; }
}
