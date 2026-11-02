// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Represents a JSON Web Token (JWT) parsed from the compact serialization, with support for either JWS or JWE
/// protection.
/// </summary>
/// <param name="protectionType">Contains a value indicating how the JWT is protected, either <c>JWS</c> or <c>JWE</c>.</param>
/// <param name="header">Contains the deserialized JOSE Protected Header from the JWT.</param>
/// <param name="encodedToken">Contains the original encoded compact JWT value.</param>
/// <param name="segments">Contains the dot-separated segments of the compact token, as slices over
/// <paramref name="encodedToken"/> (no copies are made).</param>
[PublicAPI]
public sealed class CompactJwt(
    string protectionType,
    JsonElement header,
    string encodedToken,
    IReadOnlyList<ReadOnlyMemory<char>> segments
) : JoseToken
{
    /// <inheritdoc />
    public override string ProtectionType { get; } = protectionType;

    /// <inheritdoc />
    public override JoseSerializationFormat SerializationFormat => JoseSerializationFormat.Compact;

    /// <summary>
    /// Gets the dot-separated segments of the compact token, as slices over the original encoded token.
    /// A JWS token has three segments and a JWE token has five.
    /// </summary>
    public IReadOnlyList<ReadOnlyMemory<char>> Segments { get; } = segments;

    /// <inheritdoc />
    public override JsonElement Header { get; } = header;

    /// <inheritdoc />
    public override string ToString() => encodedToken;
}
