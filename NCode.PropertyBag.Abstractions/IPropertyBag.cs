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

using JetBrains.Annotations;

namespace NCode.PropertyBag;

/// <summary>
/// Provides a strongly typed collection of properties that can be accessed by key.
/// </summary>
[PublicAPI]
public interface IPropertyBag : IReadOnlyPropertyBag
{
    /// <summary>
    /// Sets a strongly typed value for the specified <paramref name="key"/> in the property bag.
    /// </summary>
    /// <param name="key">The key of the strongly typed value to set in the property bag.</param>
    /// <param name="value">The strongly typed value to set in the property bag.</param>
    /// <typeparam name="T">The type of the value to set in the property bag.</typeparam>
    /// <returns>The <see cref="IPropertyBag"/> instance for method chaining.</returns>
    IPropertyBag Set<T>(PropertyBagKey<T> key, T value);

    /// <summary>
    /// Removes the value associated with the specified <paramref name="key"/> from the property bag.
    /// </summary>
    /// <param name="key">The key of the strongly typed value to remove from the property bag.</param>
    /// <returns>The <see cref="IPropertyBag"/> instance for method chaining.</returns>
    IPropertyBag Remove(PropertyBagKey key);

    /// <summary>
    /// Temporarily sets a strongly typed value for the specified <paramref name="key"/> in the property bag
    /// that will be removed when the returned <see cref="IPropertyBagScope"/> is disposed.
    /// </summary>
    /// <param name="key">The key of the strongly typed value to set in the property bag.</param>
    /// <param name="value">The strongly typed value to set in the property bag.</param>
    /// <typeparam name="T">The type of the value to set in the property bag.</typeparam>
    /// <returns>The <see cref="IPropertyBagScope"/> instance which will remove the value when disposed.</returns>
    IPropertyBagScope Scope<T>(PropertyBagKey<T> key, T value);
}
