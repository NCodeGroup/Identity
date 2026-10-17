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

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Provides the ability to validate a JSON Web Token (JWT) that is encoded using JWS or JWE compact serialization format.
/// See the following RFCs for more information:
/// https://datatracker.ietf.org/doc/html/rfc7519
/// https://datatracker.ietf.org/doc/html/rfc7515
/// </summary>
[PublicAPI]
public interface IJwtValidator
{
    /// <summary>
    /// Validates a JSON Web Token (JWT) that is encoded using JWS or JWE compact serialization format.
    /// </summary>
    /// <param name="token">The JSON Web Token (JWT) to validate.</param>
    /// <param name="parameters">A <see cref="JwtValidationParameters"/> instance that specifies how to perform the validation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// results of the validated JWT.</returns>
    ValueTask<JwtValidationResult> ValidateAsync(
        string token,
        JwtValidationParameters parameters,
        CancellationToken cancellationToken
    );
}
