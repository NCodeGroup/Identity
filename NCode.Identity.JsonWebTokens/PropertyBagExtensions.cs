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

using System.Runtime.CompilerServices;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Contains extension methods for the <see cref="PropertyBag"/> class.
/// </summary>
public static class PropertyBagExtensions
{
    /// <summary>
    /// Sets a strongly typed value in the property bag by inferring the name of key using <see cref="CallerArgumentExpressionAttribute"/>.
    /// </summary>
    /// <param name="bag">The <see cref="PropertyBag"/> instance.</param>
    /// <param name="value">The strongly typed value to set in the property bag.</param>
    /// <param name="name">The <see cref="string"/> name of the value in the property bag.</param>
    /// <typeparam name="T">The type of the value in the property bag.</typeparam>
    /// <returns>The <see cref="PropertyBag"/> instance for method chaining.</returns>
    public static PropertyBag Set<T>(this PropertyBag bag, T value, [CallerArgumentExpression("value")] string? name = null) =>
        bag.Set(new PropertyBagKey<T>(name ?? string.Empty), value);
}
