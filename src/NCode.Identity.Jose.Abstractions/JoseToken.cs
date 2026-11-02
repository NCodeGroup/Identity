// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
