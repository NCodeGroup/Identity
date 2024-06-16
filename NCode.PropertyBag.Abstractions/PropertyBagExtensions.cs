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
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace NCode.PropertyBag;

/// <summary>
/// Contains extension methods for the <see cref="IPropertyBag"/> abstraction.
/// </summary>
[PublicAPI]
public static class PropertyBagExtensions
{
    /// <summary>
    /// Sets a strongly typed value in the property bag by inferring the name of key using <see cref="CallerArgumentExpressionAttribute"/>.
    /// </summary>
    /// <param name="bag">The <see cref="IPropertyBag"/> instance.</param>
    /// <param name="value">The strongly typed value to set in the property bag.</param>
    /// <param name="name">The <see cref="string"/> name of the value in the property bag.</param>
    /// <typeparam name="T">The type of the value in the property bag.</typeparam>
    /// <returns>The <see cref="IPropertyBag"/> instance for method chaining.</returns>
    public static IPropertyBag Set<T>(this IPropertyBag bag, T value, [CallerArgumentExpression(nameof(value))] string? name = null) =>
        bag.Set(new PropertyBagKey<T>(name ?? string.Empty), value);

    /// <summary>
    /// Attempts to get a strongly typed value from the property bag by inferring the name of key using <see cref="CallerArgumentExpressionAttribute"/>.
    /// </summary>
    /// <param name="bag">The <see cref="IPropertyBag"/> instance.</param>
    /// <param name="value">When this method returns, contains the strongly typed value associated with the specified key,
    /// it the key is found; otherwise, the default value for the type of the <paramref name="value"/> parameter.
    /// This parameter is passed uninitialized.</param>
    /// <param name="name">The <see cref="string"/> name of the value in the property bag.</param>
    /// <typeparam name="T">The type of the value to get from the property bag.</typeparam>
    /// <returns><c>true</c> if the <see cref="IPropertyBag"/> contains an element with the specified key; otherwise,
    /// <c>false</c>.</returns>
    public static bool TryGet<T>(this IReadOnlyPropertyBag bag, [MaybeNullWhen(false)] out T value, [CallerArgumentExpression(nameof(value))] string? name = null) =>
        bag.TryGetValue(new PropertyBagKey<T>(name ?? string.Empty), out value);
}
