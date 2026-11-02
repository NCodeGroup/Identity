// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
