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
