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

using System.Buffers;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides the ability to encode and decode JWT values using JSON Object Signing and Encryption (JOSE).
/// </summary>
/// <remarks>
/// All decode, deserialize, and verify operations are centered on the parsed <see cref="JoseToken"/>: parse once
/// (which also exposes the JOSE <see cref="JoseToken.Header"/> before validation/decryption), then operate on the
/// token. One-shot overloads that accept a raw token <see cref="string"/> are provided for convenience. The header is
/// always read from <see cref="JoseToken.Header"/> rather than returned via <c>out</c> parameters.
/// </remarks>
[PublicAPI]
public interface IJoseSerializer
{
    /// <summary>
    /// Parses a JSON Web Token (JWT) and returns the parsed token. Both the compact and JSON (flattened/general)
    /// serializations, and both JWS and JWE protection, are supported. The main purpose of this method is to extract
    /// the JOSE header (via <see cref="JoseToken.Header"/>) before validation/decryption.
    /// </summary>
    /// <param name="token">The JSON Web Token (JWT) to parse.</param>
    /// <returns>The parsed <see cref="JoseToken"/>.</returns>
    JoseToken Parse(string token);

    /// <summary>
    /// Validates a JSON Web Token (JWT) and writes the decoded payload to the specified buffer.
    /// </summary>
    /// <param name="token">The parsed <see cref="JoseToken"/> to decode and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <param name="payload">The destination buffer that receives the decoded payload.</param>
    void Decode(JoseToken token, SecretKey secretKey, IBufferWriter<byte> payload);

    /// <summary>
    /// Validates a JSON Web Token (JWT) and returns the decoded payload as a <see cref="string"/>.
    /// </summary>
    /// <param name="token">The parsed <see cref="JoseToken"/> to decode and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <returns>The decoded payload from the JSON Web Token (JWT).</returns>
    string Decode(JoseToken token, SecretKey secretKey);

    /// <summary>
    /// Parses, validates, and returns the decoded payload of a JSON Web Token (JWT) as a <see cref="string"/>.
    /// </summary>
    /// <param name="token">The JSON Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <returns>The decoded payload from the JSON Web Token (JWT).</returns>
    string Decode(string token, SecretKey secretKey);

    /// <summary>
    /// Validates a JSON Web Token (JWT) and returns the deserialized payload using the serializer's configured options.
    /// </summary>
    /// <param name="token">The parsed <see cref="JoseToken"/> to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from the JSON Web Token (JWT).</returns>
    T? Deserialize<T>(JoseToken token, SecretKey secretKey);

    /// <summary>
    /// Validates a JSON Web Token (JWT) and returns the deserialized payload.
    /// </summary>
    /// <param name="token">The parsed <see cref="JoseToken"/> to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <param name="jsonOptions">The options to control JSON deserialization behavior.
    /// When <c>null</c>, the serializer's configured options are used.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from the JSON Web Token (JWT).</returns>
    T? Deserialize<T>(JoseToken token, SecretKey secretKey, JsonSerializerOptions? jsonOptions);

    /// <summary>
    /// Parses, validates, and returns the deserialized payload of a JSON Web Token (JWT) using the serializer's
    /// configured options.
    /// </summary>
    /// <param name="token">The JSON Web Token (JWT) to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from the JSON Web Token (JWT).</returns>
    T? Deserialize<T>(string token, SecretKey secretKey);

    /// <summary>
    /// Parses, validates, and returns the deserialized payload of a JSON Web Token (JWT).
    /// </summary>
    /// <param name="token">The JSON Web Token (JWT) to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <param name="jsonOptions">The options to control JSON deserialization behavior.
    /// When <c>null</c>, the serializer's configured options are used.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from the JSON Web Token (JWT).</returns>
    T? Deserialize<T>(string token, SecretKey secretKey, JsonSerializerOptions? jsonOptions);

    /// <summary>
    /// Validates a JWS-protected JSON Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The parsed <see cref="JoseToken"/> to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(JoseToken token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload);

    /// <summary>
    /// Validates a JWS-protected JSON Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The parsed <see cref="JoseToken"/> to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(JoseToken token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload);

    /// <summary>
    /// Validates a JWS-protected JSON Web Token (JWT) with a strongly-typed detached payload using the serializer's
    /// configured options. This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The parsed <see cref="JoseToken"/> to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <typeparam name="T">The type of the detached payload.</typeparam>
    void VerifyJws<T>(JoseToken token, SecretKey secretKey, T detachedPayload);

    /// <summary>
    /// Validates a JWS-protected JSON Web Token (JWT) with a strongly-typed detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The parsed <see cref="JoseToken"/> to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.
    /// When <c>null</c>, the serializer's configured options are used.</param>
    /// <typeparam name="T">The type of the detached payload.</typeparam>
    void VerifyJws<T>(
        JoseToken token,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions
    );

    /// <summary>
    /// Parses and validates a JWS-protected JSON Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The JSON Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload);

    /// <summary>
    /// Parses and validates a JWS-protected JSON Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The JSON Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload);

    /// <summary>
    /// Parses and validates a JWS-protected JSON Web Token (JWT) with a strongly-typed detached payload using the
    /// serializer's configured options. This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The JSON Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <typeparam name="T">The type of the detached payload.</typeparam>
    void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload);

    /// <summary>
    /// Parses and validates a JWS-protected JSON Web Token (JWT) with a strongly-typed detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The JSON Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.
    /// When <c>null</c>, the serializer's configured options are used.</param>
    /// <typeparam name="T">The type of the detached payload.</typeparam>
    void VerifyJws<T>(
        string token,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions
    );

    /// <summary>
    /// Creates a new <see cref="JoseEncoder"/> with the specified signing options.
    /// The returned encoder is the single entry point for encoding JWS tokens, in any serialization format.
    /// </summary>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <returns>The newly created <see cref="JoseEncoder"/> instance.</returns>
    JoseEncoder CreateEncoder(JoseSigningOptions signingOptions);

    /// <summary>
    /// Creates a new <see cref="JoseEncoder"/> with the specified encryption options.
    /// The returned encoder is the single entry point for encrypting JWE tokens, in any serialization format.
    /// </summary>
    /// <param name="encryptionOptions">The JOSE encryption options.</param>
    /// <returns>The newly created <see cref="JoseEncoder"/> instance.</returns>
    JoseEncoder CreateEncoder(JoseEncryptionOptions encryptionOptions);
}
