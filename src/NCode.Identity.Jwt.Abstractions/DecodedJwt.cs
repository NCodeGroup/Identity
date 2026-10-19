#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Identity.Jose;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jwt;

/// <summary>
/// Represents a signed JSON Web Token (JWT) that has been successfully decoded: a JWS, or the JWS that a Nested JWT
/// encloses in a JWE (<c>RFC 7519</c> §5.2).
/// </summary>
/// <param name="token">The parsed JWS whose signature was verified.</param>
/// <param name="payload">The deserialized payload (the JWT Claims Set) from the JWS.</param>
/// <param name="signingKey">The <see cref="SecretKey"/> that verified the signature of the JWS.</param>
[PublicAPI]
public sealed class DecodedJwt(JoseToken token, JsonElement payload, SecretKey signingKey)
{
    /// <summary>
    /// Gets the parsed JWS whose signature was verified.
    /// </summary>
    private JoseToken Token { get; } = token;

    /// <summary>
    /// Gets the encoded JWS whose signature was verified. For a Nested JWT this is the JWS the enclosing JWE carried,
    /// not the encrypted token as presented.
    /// </summary>
    public string EncodedToken => Token.ToString();

    /// <summary>
    /// Gets the deserialized header of the JWS whose signature was verified.
    /// </summary>
    public JsonElement Header => Token.Header;

    /// <summary>
    /// Gets the deserialized payload (the JWT Claims Set) from the JWS.
    /// </summary>
    public JsonElement Payload { get; } = payload;

    /// <summary>
    /// Gets the <see cref="SecretKey"/> that verified the signature of the JWS.
    /// </summary>
    public SecretKey SigningKey { get; } = signingKey;

    /// <summary>
    /// Gets the deserialized header of the JWE that enclosed the JWS when the token was a Nested JWT, or <c>null</c>
    /// when the token was not encrypted.
    /// </summary>
    public JsonElement? EncryptionHeader { get; init; }

    /// <summary>
    /// Gets the <see cref="SecretKey"/> that decrypted the enclosing JWE when the token was a Nested JWT, or
    /// <c>null</c> when the token was not encrypted.
    /// </summary>
    public SecretKey? DecryptionKey { get; init; }
}
