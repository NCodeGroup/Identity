﻿#region Copyright Preamble

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
using System.Text.Json.Serialization;
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides a <see cref="JsonConverterFactory"/> implementation that can serialize and deserialize <see cref="IOpenIdMessage"/>
/// instances to and from JSON.
/// </summary>
public class OpenIdMessageJsonConverterFactory(
    OpenIdEnvironment openIdEnvironment
) : JsonConverterFactory
{
    private OpenIdEnvironment OpenIdEnvironment { get; } = openIdEnvironment;

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(IOpenIdMessage).IsAssignableFrom(typeToConvert);
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var typeOfConverter = typeof(OpenIdMessageJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(typeOfConverter, OpenIdEnvironment);
    }
}
