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

using System.Text.Json;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Servers;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides the ability parse and load a <see cref="Parameter"/> given its string values.
/// </summary>
/// <typeparam name="T">The type of parameter to parse.</typeparam>
public abstract class ParameterParser<T> : ParameterLoader, IJsonParser
{
    /// <summary>
    /// Gets the value which is used to delimit string lists in <c>OAuth</c> and <c>OpenID Connect</c> parameters.
    /// The default value is the space ' ' character.
    /// </summary>
    public virtual string Separator => OpenIdConstants.ParameterSeparatorString;

    /// <summary>
    /// Gets the <see cref="StringComparison"/> that should be used when comparing string values.
    /// The default value is <see cref="StringComparison.Ordinal"/>.
    /// </summary>
    public virtual StringComparison StringComparison => StringComparison.Ordinal;

    /// <summary>
    /// Returns the specified <paramref name="value"/> formatted as <see cref="StringValues"/>.
    /// </summary>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> to use when serializing the value.</param>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The value formatted as <see cref="StringValues"/>.</returns>
    public abstract StringValues Serialize(OpenIdServer openIdServer, T? value);

    /// <summary>
    /// Parses the specified string values into a type-specific value.
    /// </summary>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> to parse.</param>
    /// <returns>The parsed type-specific value.</returns>
    public abstract T Parse(
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        StringValues stringValues);

    /// <summary>
    /// Parses and loads a <see cref="Parameter"/> given its string values.
    /// </summary>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> to parse.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    public override Parameter Load(
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        StringValues stringValues)
    {
        var parsedValue = Parse(openIdServer, descriptor, stringValues);
        return Load(openIdServer, descriptor, stringValues, parsedValue);
    }

    /// <inheritdoc />
    public virtual Parameter Read(
        ref Utf8JsonReader reader,
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options)
    {
        var parsedValue = JsonSerializer.Deserialize<T>(ref reader, options);
        var stringValues = Serialize(openIdServer, parsedValue);
        return descriptor.Loader.Load(openIdServer, descriptor, stringValues, parsedValue);
    }

    /// <inheritdoc />
    public virtual void Write(
        Utf8JsonWriter writer,
        OpenIdServer openIdServer,
        Parameter parameter,
        JsonSerializerOptions options)
    {
        var typedParameter = (Parameter<T?>)parameter;
        writer.WritePropertyName(parameter.Descriptor.ParameterName);
        JsonSerializer.Serialize(writer, typedParameter.ParsedValue, options);
    }
}
