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
public abstract class ParameterLoader : IParameterLoader
{
    /// <summary>
    /// Gets the default <see cref="ParameterLoader"/> instance that can be used to load and parse unknown parameter types.
    /// </summary>
    public static ParameterLoader Default => ParameterParsers.StringValues;

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
