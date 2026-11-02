// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using ParameterDescriptor = NCode.Identity.OpenId.Messages.Parameters.ParameterDescriptor;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides the ability parse and load <see cref="IParameter"/> values.
/// </summary>
[PublicAPI]
public interface IParameterLoader
{
    /// <summary>
    /// Gets the type of parameter that this instance can parse and load.
    /// </summary>
    Type ParameterType { get; }

    /// <summary>
    /// Creates a <see cref="IParameter"/> given its parsed value.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use while creating the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to create.</param>
    /// <param name="parser">The <see cref="IParameterParser{T}"/> to use while creating the parameter.</param>
    /// <param name="parsedValue">The parsed value for the parameter.</param>
    /// <returns>The newly created parameter.</returns>
    IParameter<T> Create<T>(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        IParameterParser<T> parser,
        T? parsedValue
    );

    /// <summary>
    /// Parses and loads a <see cref="IParameter"/> given string values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use while loading the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to load.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> to parse.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    IParameter Load(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    );

    /// <summary>
    /// Parses and loads a <see cref="IParameter"/> given JSON.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="format">The serialization format of the parameter being parsed.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    IParameter Read(
        ref Utf8JsonReader reader,
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        SerializationFormat format,
        JsonSerializerOptions options
    );

    /// <summary>
    /// Serializes the specified <see cref="IParameter"/> to JSON.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use when serializing the value.</param>
    /// <param name="parameter">The <see cref="IParameter"/> to serialize.</param>
    /// <param name="format">The serialization format to use.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    void Write(
        Utf8JsonWriter writer,
        OpenIdEnvironment openIdEnvironment,
        IParameter parameter,
        SerializationFormat format,
        JsonSerializerOptions options
    );
}
