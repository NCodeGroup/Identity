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
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides the ability parse and load a <see cref="Parameter"/> given its string values.
/// </summary>
/// <typeparam name="T">The type of parameter to parse.</typeparam>
public abstract class ParameterParser<T> : ParameterLoader, IJsonParser
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
    /// <param name="context">The <see cref="IOpenIdContext"/> to use when serializing the value.</param>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The value formatted as <see cref="StringValues"/>.</returns>
    public abstract StringValues Serialize(IOpenIdContext context, T? value);

    /// <summary>
    /// Parses the specified string values into a type-specific value.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdContext"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> to parse.</param>
    /// <param name="ignoreErrors">Specifies whether errors during parsing should be ignored.</param>
    /// <returns>The parsed type-specific value.</returns>
    public abstract T Parse(IOpenIdContext context, ParameterDescriptor descriptor, StringValues stringValues, bool ignoreErrors = false);

    /// <summary>
    /// Parses and loads a <see cref="Parameter"/> given its string values.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdContext"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> to parse.</param>
    /// <param name="ignoreErrors">Specifies whether errors during parsing should be ignored.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    public override Parameter Load(IOpenIdContext context, ParameterDescriptor descriptor, StringValues stringValues, bool ignoreErrors = false)
    {
        var parsedValue = Parse(context, descriptor, stringValues, ignoreErrors);
        return new Parameter<T>(descriptor, stringValues, parsedValue);
    }

    /// <inheritdoc />
    public virtual Parameter Read(ref Utf8JsonReader reader, IOpenIdContext context, ParameterDescriptor descriptor, JsonSerializerOptions options)
    {
        var parsedValue = JsonSerializer.Deserialize<T?>(ref reader, options);
        var stringValues = Serialize(context, parsedValue);
        return descriptor.Loader.Load(context, descriptor, stringValues, parsedValue);
    }

    /// <inheritdoc />
    public virtual void Write(Utf8JsonWriter writer, IOpenIdContext context, Parameter parameter, JsonSerializerOptions options)
    {
        var typedParameter = (Parameter<T?>)parameter;
        writer.WritePropertyName(parameter.Descriptor.ParameterName);
        JsonSerializer.Serialize(writer, typedParameter.ParsedValue, options);
    }
}
