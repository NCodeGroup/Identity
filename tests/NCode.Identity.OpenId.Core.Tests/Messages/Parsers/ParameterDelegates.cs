// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

internal delegate IParameter CreateParameterDelegate(
    OpenIdEnvironment openIdEnvironment,
    ParameterDescriptor descriptor,
    IParameterLoader parser,
    object? parsedValue
);

internal delegate IParameter LoadParameterDelegate(
    OpenIdEnvironment openIdEnvironment,
    ParameterDescriptor descriptor,
    StringValues stringValues
);

internal delegate IParameter ReadParameterDelegate(
    OpenIdEnvironment environment,
    ParameterDescriptor descriptor,
    SerializationFormat format,
    JsonSerializerOptions options
);

internal delegate void WriteParameterDelegate(
    Utf8JsonWriter writer,
    OpenIdEnvironment openIdEnvironment,
    IParameter parameter,
    SerializationFormat format,
    JsonSerializerOptions options
);

internal delegate StringValues SerializeParameterDelegate<in T>(
    OpenIdEnvironment openIdEnvironment,
    ParameterDescriptor descriptor,
    T? parsedValue
);

internal delegate T ParseParameterDelegate<out T>(
    OpenIdEnvironment openIdEnvironment,
    ParameterDescriptor descriptor,
    StringValues stringValues
);

[return: NotNullIfNotNull("value")]
internal delegate T? CloneParameterDelegate<T>(T? value);
