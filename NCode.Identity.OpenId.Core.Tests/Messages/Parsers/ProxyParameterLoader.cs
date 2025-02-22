﻿#region Copyright Preamble

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
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

[PublicAPI]
internal class ProxyParameterLoader : IParameterLoader
{
    public CreateParameterDelegate? CreateCallback { get; set; }
    public LoadParameterDelegate? LoadCallback { get; set; }
    public ReadParameterDelegate? ReadCallback { get; set; }

    public IParameter<T> Create<T>(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues,
        T? parsedValue
    ) =>
        (IParameter<T>?)CreateCallback?.Invoke(openIdEnvironment, descriptor, stringValues, parsedValue) ??
        throw new NotImplementedException();

    public IParameter Load(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    ) =>
        LoadCallback?.Invoke(openIdEnvironment, descriptor, stringValues) ??
        throw new NotImplementedException();

    public IParameter Read(
        ref Utf8JsonReader reader,
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        SerializationFormat format,
        JsonSerializerOptions options
    ) =>
        ReadCallback?.Invoke(openIdEnvironment, descriptor, format, options) ??
        throw new NotImplementedException();
}
