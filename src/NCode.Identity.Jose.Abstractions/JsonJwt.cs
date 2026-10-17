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

namespace NCode.Identity.Jose;

/// <summary>
/// Represents a JSON Web Token (JWT) parsed from one of the JSON serializations (flattened or general),
/// as defined by <c>RFC 7515</c> §7.2 (JWS) and <c>RFC 7516</c> §7.2 (JWE).
/// </summary>
/// <param name="protectionType">Contains a value indicating how the JWT is protected, either <c>JWS</c> or <c>JWE</c>.</param>
/// <param name="serializationFormat">Contains a value indicating the JSON serialization format.</param>
/// <param name="encodedToken">Contains the original encoded JWT value.</param>
/// <param name="root">Contains the parsed root JSON object of the JWT.</param>
/// <param name="header">Contains the primary JOSE header of the JWT.</param>
[PublicAPI]
public sealed class JsonJwt(
    string protectionType,
    JoseSerializationFormat serializationFormat,
    string encodedToken,
    JsonElement root,
    JsonElement header
) : JoseToken
{
    /// <inheritdoc />
    public override string ProtectionType { get; } = protectionType;

    /// <inheritdoc />
    public override JoseSerializationFormat SerializationFormat { get; } = serializationFormat;

    /// <summary>
    /// Gets the parsed root JSON object of the JWT.
    /// </summary>
    public JsonElement Root { get; } = root;

    /// <inheritdoc />
    public override JsonElement Header { get; } = header;

    /// <inheritdoc />
    public override string ToString() => encodedToken;
}
