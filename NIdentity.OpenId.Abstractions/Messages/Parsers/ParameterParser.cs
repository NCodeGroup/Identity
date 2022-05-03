#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides the ability parse and load a <see cref="Parameter"/> given its string values.
/// </summary>
/// <typeparam name="T">The type of parameter to parse.</typeparam>
public abstract class ParameterParser<T> : ParameterLoader
{
    /// <summary>
    /// Gets the space ' ' character which is used to delimit string lists in <c>OAuth</c> and <c>OpenID Connect</c> parameters.
    /// </summary>
    public virtual string Separator => OpenIdConstants.ParameterSeparator;

    /// <summary>
    /// Gets the case-sensitive <see cref="StringComparison"/> that should be used when comparing string values.
    /// </summary>
    public virtual StringComparison StringComparison => StringComparison.Ordinal;

    /// <summary>
    /// Returns the specified <paramref name="value"/> formatted as <see cref="StringValues"/>.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdMessageContext"/> to use when serializing the value.</param>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The value formatted as <see cref="StringValues"/>.</returns>
    public abstract StringValues Serialize(IOpenIdMessageContext context, T value);

    /// <summary>
    /// Parses the specified string values into a type-specific value.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdMessageContext"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> to parse.</param>
    /// <returns>The parsed type-specific value.</returns>
    public abstract T Parse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues);

    /// <summary>
    /// Parses and loads a <see cref="Parameter"/> given its string values.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdMessageContext"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> to parse.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    public override Parameter Load(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues)
    {
        var parsedValue = Parse(context, descriptor, stringValues);
        return new Parameter(descriptor, stringValues, parsedValue);
    }
}