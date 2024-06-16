#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides the ability to create and validate a Json Web Token (JWT).
/// See the following RFCs for more information:
/// https://datatracker.ietf.org/doc/html/rfc7519
/// https://datatracker.ietf.org/doc/html/rfc7515
/// </summary>
[PublicAPI]
public interface IJsonWebTokenService
{
    /// <summary>
    /// Encodes a Json Web Token (JWT) using JWS or JWE compact serialization format.
    /// </summary>
    /// <param name="parameters">The <see cref="EncodeJwtParameters"/> instance that specifies how to encode the token.</param>
    /// <returns>The encoded JWT in compact serialization format.</returns>
    string EncodeJwt(EncodeJwtParameters parameters);

    /// <summary>
    /// Validates a Json Web Token (JWT) that is encoded using JWS or JWE compact serialization format.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="parameters">An <see cref="ValidateJwtParameters"/> instance that specifies how to perform the validation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// results of the validated JWT.</returns>
    ValueTask<ValidateJwtResult> ValidateJwtAsync(
        string token,
        ValidateJwtParameters parameters,
        CancellationToken cancellationToken);
}
