// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Encoding;

/// <summary>
/// Provides the ability to encode a JSON Web Token (JWT) using JWS or JWE compact serialization format.
/// See the following RFCs for more information:
/// https://datatracker.ietf.org/doc/html/rfc7519
/// https://datatracker.ietf.org/doc/html/rfc7515
/// </summary>
[PublicAPI]
public interface IJwtEncoder
{
    /// <summary>
    /// Encodes a JSON Web Token (JWT) using JWS or JWE compact serialization format.
    /// </summary>
    /// <param name="parameters">The <see cref="JwtEncodingParameters"/> instance that specifies how to encode the token.</param>
    /// <returns>The encoded JWT in compact serialization format.</returns>
    string Encode(JwtEncodingParameters parameters);
}
