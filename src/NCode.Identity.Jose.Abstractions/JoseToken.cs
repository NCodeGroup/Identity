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
/// Represents a parsed JSON Web Token (JWT), either JWS- or JWE-protected, in any serialization format.
/// This is the single hub for decode, deserialize, and verify operations on <see cref="IJoseSerializer"/>; the
/// JOSE <see cref="Header"/> is exposed here so it can be inspected <b>before</b> validation/decryption (for example
/// to resolve the <c>kid</c> / <c>alg</c> used to pick a key).
/// </summary>
[PublicAPI]
public abstract class JoseToken
{
    /// <summary>
    /// Gets a value indicating how the JWT is protected, either <c>JWS</c> or <c>JWE</c>.
    /// See <see cref="JoseProtectionTypes"/>.
    /// </summary>
    public abstract string ProtectionType { get; }

    /// <summary>
    /// Gets a value indicating the wire serialization of the JWT (compact, flattened JSON, or general JSON).
    /// </summary>
    public abstract JoseSerializationFormat SerializationFormat { get; }

    /// <summary>
    /// Gets the primary JOSE header of the JWT as a <see cref="JsonElement"/>. For the compact serialization this is
    /// the JOSE Protected Header; for the JSON serializations this is the protected header of the first
    /// signature/recipient merged with the shared/unprotected members. The header is available before the token is
    /// decrypted or verified.
    /// </summary>
    public abstract JsonElement Header { get; }

    /// <summary>
    /// Returns the original encoded JSON Web Token (JWT) value.
    /// </summary>
    public abstract override string ToString();
}
