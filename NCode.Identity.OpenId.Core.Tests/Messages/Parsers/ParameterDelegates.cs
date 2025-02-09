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
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

internal delegate IParameter CreateParameterDelegate(
    OpenIdEnvironment openIdEnvironment,
    ParameterDescriptor descriptor,
    StringValues stringValues,
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
