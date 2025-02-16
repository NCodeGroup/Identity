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
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides the ability parse and load <see cref="IParameter"/> values of a specific type.
/// </summary>
/// <typeparam name="T">The type of parameter to parse.</typeparam>
[PublicAPI]
public interface IParameterParser<T> : IParameterLoader
{
    /// <summary>
    /// Returns the specified <paramref name="parsedValue"/> formatted as <see cref="StringValues"/>.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use when serializing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to serialize.</param>
    /// <param name="parsedValue">The value to serialize.</param>
    /// <returns>The value formatted as <see cref="StringValues"/>.</returns>
    StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T? parsedValue
    );

    /// <summary>
    /// Parses the specified string values into a type-specific value.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> to parse.</param>
    /// <returns>The parsed type-specific value.</returns>
    T Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    );

    /// <summary>
    /// Clones the specified <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to clone.</param>
    /// <returns>The cloned value.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    T? Clone(T? value);
}
