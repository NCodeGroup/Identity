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

using System.Text.Json;
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
public abstract class ParameterParser<T> : ParameterLoader, IParameterParser<T>
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

    /// <inheritdoc />
    public abstract StringValues Serialize(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T? parsedValue);

    /// <inheritdoc />
    public abstract T Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues);

    /// <inheritdoc />
    public override IParameter Load(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues)
    {
        var parsedValue = Parse(openIdEnvironment, descriptor, stringValues);
        return Create(openIdEnvironment, descriptor, stringValues, parsedValue);
    }

    /// <summary>
    /// Wrapper method for <c>JsonSerializer.Deserialize</c> to allow for unit testing.
    /// </summary>
    protected virtual T1? Deserialize<T1>(ref Utf8JsonReader reader, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<T1>(ref reader, options);

    /// <inheritdoc />
    public override IParameter Read(
        ref Utf8JsonReader reader,
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        SerializationFormat format,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && format != SerializationFormat.Json)
        {
            var stringValues = Deserialize<StringValues>(ref reader, options);
            var parsedValue = Parse(openIdEnvironment, descriptor, stringValues);
            return Create(openIdEnvironment, descriptor, stringValues, parsedValue);
        }
        else
        {
            var parsedValue = Deserialize<T>(ref reader, options);
            var stringValues = Serialize(openIdEnvironment, descriptor, parsedValue);
            return Create(openIdEnvironment, descriptor, stringValues, parsedValue);
        }
    }
}
