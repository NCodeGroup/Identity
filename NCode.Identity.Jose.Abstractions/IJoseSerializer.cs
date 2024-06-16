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
using NCode.Identity.Secrets;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides the ability to encode and decode JWT values using JSON Object Signing and Encryption (JOSE).
/// </summary>
[PublicAPI]
public partial interface IJoseSerializer
{
    /// <summary>
    /// Parses a Json Web Token (JWT) and returns the parsed JWT in compact form.
    /// This method supports both JWS and JWE (i.e. encrypted) tokens in compact form.
    /// The main purpose of this method is to extract the JWT header before validation/decryption.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to parse.</param>
    /// <returns>The parsed JWT in compact form.</returns>
    CompactJwt ParseCompactJwt(string token);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(string token, SecretKey secretKey);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload and header.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(string token, SecretKey secretKey, out JsonElement header);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to decode and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(CompactJwt compactJwt, SecretKey secretKey);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(string token, SecretKey secretKey);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload and header.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(string token, SecretKey secretKey, out JsonElement header);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(CompactJwt compactJwt, SecretKey secretKey);
}
