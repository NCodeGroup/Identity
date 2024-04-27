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

namespace NCode.PropertyBag;

/// <summary>
/// Provides a strongly typed read-only collection of properties that can be accessed by key.
/// </summary>
public interface IReadOnlyPropertyBag
{
    /// <summary>
    /// Returns a new instance of the <see cref="IPropertyBag"/> class that is a shallow copy of the current instance.
    /// </summary>
    IPropertyBag Clone();

    /// <summary>
    /// Attempts to get a strongly typed value associated with the specified key from the property bag.
    /// </summary>
    /// <param name="key">The key of the strongly typed value to get from the property bag.</param>
    /// <param name="value">When this method returns, contains the strongly typed value associated with the specified key,
    /// it the key is found; otherwise, the default value for the type of the <paramref name="value"/> parameter.
    /// This parameter is passed uninitialized.</param>
    /// <typeparam name="T">The type of the value to get from the property bag.</typeparam>
    /// <returns><c>true</c> if the <see cref="IPropertyBag"/> contains an element with the specified key; otherwise,
    /// <c>false</c>.</returns>
    bool TryGetValue<T>(PropertyBagKey<T> key, [MaybeNullWhen(false)] out T value);
}
