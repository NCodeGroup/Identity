// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
public abstract class ParameterLoader : IParameterLoader
{
    /// <summary>
    /// Gets the default <see cref="ParameterLoader"/> instance that can be used to load and parse unknown parameter types.
    /// </summary>
    public static ParameterLoader Default => CommonParameterParsers.StringValues;

    /// <inheritdoc />
    public abstract Type ParameterType { get; }

    /// <inheritdoc />
    public virtual IParameter<T> Create<T>(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        IParameterParser<T> parser,
        T? parsedValue
    ) => Parameter.Create(openIdEnvironment, descriptor, parser, parsedValue);

    /// <inheritdoc />
    public abstract IParameter Load(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    );

    /// <inheritdoc />
    public abstract IParameter Read(
        ref Utf8JsonReader reader,
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        SerializationFormat format,
        JsonSerializerOptions options
    );

    /// <inheritdoc />
    public abstract void Write(
        Utf8JsonWriter writer,
        OpenIdEnvironment openIdEnvironment,
        IParameter parameter,
        SerializationFormat format,
        JsonSerializerOptions options
    );
}
