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
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides the ability parse and load <see cref="IParameter"/> values.
/// </summary>
[PublicAPI]
public class ParameterLoader : IParameterLoader
{
    /// <summary>
    /// Gets a default implementation of <see cref="IParameterLoader"/> that simply returns a newly initialized <see cref="IParameter"/> object.
    /// </summary>
    public static ParameterLoader Default { get; } = new();

    private static StringValues ToStringValues(string? value) =>
        value switch
        {
            null => StringValues.Empty,
            _ => new StringValues(value.Split(OpenIdConstants.ParameterSeparatorChar))
        };

    private static string? FromStringValues(StringValues stringValues) =>
        stringValues.Count switch
        {
            0 => null,
            1 => stringValues[0],
            _ => string.Join(OpenIdConstants.ParameterSeparatorChar, stringValues.AsEnumerable())
        };

    /// <summary>
    /// Creates a <see cref="IParameter"/> given its string values and parsed value.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use while creating the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to create.</param>
    /// <param name="stringValues">The string values for the parameter.</param>
    /// <param name="parsedValue">The parsed value for the parameter.</param>
    /// <returns>The newly created parameter.</returns>
    public virtual IParameter<T> Create<T>(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues,
        T? parsedValue
    ) => Parameter.Create(
        openIdEnvironment,
        descriptor,
        stringValues,
        parsedValue
    );

    /// <inheritdoc />
    public virtual IParameter Load(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    ) => Create(
        openIdEnvironment,
        descriptor,
        stringValues,
        FromStringValues(stringValues)
    );

    /// <inheritdoc />
    public virtual IParameter Read(
        ref Utf8JsonReader reader,
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        SerializationFormat format,
        JsonSerializerOptions options)
    {
        switch (format)
        {
            case SerializationFormat.Json:
            case SerializationFormat.OpenId:
                if (reader.TokenType == JsonTokenType.Null)
                {
                    if (!descriptor.AllowMissingStringValues)
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (reader.TokenType != JsonTokenType.String)
                {
                    throw new InvalidOperationException();
                }

                var parsedValue = reader.GetString();

                return Create(
                    openIdEnvironment,
                    descriptor,
                    ToStringValues(parsedValue),
                    parsedValue
                );

            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }
}
