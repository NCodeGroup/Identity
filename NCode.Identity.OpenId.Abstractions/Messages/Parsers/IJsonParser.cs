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
using JetBrains.Annotations;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides the ability to parse and load JSON into a <see cref="Parameter"/> given an <see cref="Utf8JsonReader"/>.
/// </summary>
[PublicAPI]
public interface IJsonParser
{
    /// <summary>
    /// Parses and loads JSON into a <see cref="Parameter"/> given an <see cref="Utf8JsonReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    Parameter Read(
        ref Utf8JsonReader reader,
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options);

    /// <summary>
    /// Serializes the JSON value from a <see cref="Parameter"/> into the given <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use when serializing the value.</param>
    /// <param name="parameter">The <see cref="Parameter"/> to serialize as JSON.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    void Write(
        Utf8JsonWriter writer,
        OpenIdEnvironment openIdEnvironment,
        Parameter parameter,
        JsonSerializerOptions options);
}
