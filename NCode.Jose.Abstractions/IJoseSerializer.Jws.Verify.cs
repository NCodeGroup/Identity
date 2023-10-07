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
using NCode.Jose.SecretKeys;

namespace NCode.Jose;

partial interface IJoseSerializer
{
    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(
        string token,
        SecretKey secretKey,
        ReadOnlySpan<byte> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    void VerifyJws(
        string token,
        SecretKey secretKey,
        ReadOnlySpan<byte> detachedPayload,
        out JsonElement header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(
        CompactJwt compactJwt,
        SecretKey secretKey,
        ReadOnlySpan<byte> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(
        string token,
        SecretKey secretKey,
        string detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    void VerifyJws(
        string token,
        SecretKey secretKey,
        string detachedPayload,
        out JsonElement header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(
        CompactJwt compactJwt,
        SecretKey secretKey,
        string detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(
        string token,
        SecretKey secretKey,
        ReadOnlySpan<char> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    void VerifyJws(
        string token,
        SecretKey secretKey,
        ReadOnlySpan<char> detachedPayload,
        out JsonElement header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(
        CompactJwt compactJwt,
        SecretKey secretKey,
        ReadOnlySpan<char> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <typeparam name="T">The type of the payload to validate.</typeparam>
    void VerifyJws<T>(
        string token,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <typeparam name="T">The type of the payload to validate.</typeparam>
    void VerifyJws<T>(
        string token,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions,
        out JsonElement header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <typeparam name="T">The type of the payload to validate.</typeparam>
    void VerifyJws<T>(
        CompactJwt compactJwt,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions);
}
