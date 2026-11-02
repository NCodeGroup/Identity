// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
