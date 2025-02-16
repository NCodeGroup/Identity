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
    /// <inheritdoc />
    public override Type ParameterType => typeof(T);

    /// <inheritdoc />
    public abstract StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T? parsedValue
    );

    /// <inheritdoc />
    public abstract T Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    );

    /// <inheritdoc />
    public override IParameter Load(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        var parsedValue = Parse(openIdEnvironment, descriptor, stringValues);
        return Create(openIdEnvironment, descriptor, this, parsedValue);
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
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.String && format != SerializationFormat.Json)
        {
            var stringValues = Deserialize<StringValues>(ref reader, options);
            var parsedValue = Parse(openIdEnvironment, descriptor, stringValues);
            return Create(openIdEnvironment, descriptor, this, parsedValue);
        }
        else
        {
            var parsedValue = Deserialize<T>(ref reader, options);
            return Create(openIdEnvironment, descriptor, this, parsedValue);
        }
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        OpenIdEnvironment openIdEnvironment,
        IParameter parameter,
        SerializationFormat format,
        JsonSerializerOptions options
    )
    {
        var parameterName = parameter.Descriptor.ParameterName;
        writer.WritePropertyName(parameterName);

        switch (format)
        {
            case SerializationFormat.Json:
                var parsedValue = ((IParameter<T>)parameter).ParsedValue;
                JsonSerializer.Serialize(writer, parsedValue, options);
                break;

            case SerializationFormat.OpenId:
                var stringValues = parameter.GetStringValues(openIdEnvironment);
                JsonSerializer.Serialize(writer, stringValues, options);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    /// <inheritdoc />
    [return: NotNullIfNotNull(nameof(value))]
    public virtual T? Clone(T? value) =>
        value is ICloneable cloneable ? (T)cloneable.Clone() : value;
}
