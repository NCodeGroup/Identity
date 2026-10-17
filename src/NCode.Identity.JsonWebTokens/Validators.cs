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

using System.Collections.Frozen;
using System.Diagnostics;
using JetBrains.Annotations;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.JsonWebTokens.Exceptions;
using NCode.Identity.JsonWebTokens.Options;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides the ability to configure various JSON Web Token (JWT) validators.
/// </summary>
[PublicAPI]
public static class Validators
{
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    extension(ValidateJwtParameters parameters)
    {
        /// <summary>
        /// Adds a validator that asserts the <c>iss</c> claim exists in the JSON Web Token (JWT) payload with any of the
        /// specified <paramref name="validIssuers"/>.
        /// </summary>
        /// <param name="validIssuers">The collection of allowable values for the <c>iss></c> claim.</param>
        /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
        public ValidateJwtParameters ValidateIssuer(params IEnumerable<string> validIssuers) =>
            parameters.ValidateClaim(
                JoseClaimNames.Payload.Iss,
                usePayload: true,
                allowCollection: false,
                validIssuers
            );

        /// <summary>
        /// Adds a validator that asserts the JSON Web Token (JWT) carries an explicit <c>typ</c> JOSE header whose
        /// media type matches any of the specified <paramref name="validTokenTypes"/>. This is the explicit-typing
        /// defense against token-substitution / cross-JWT-confusion attacks recommended by <c>RFC 8725</c> §3.11 and
        /// mandated for JWT access tokens by <c>RFC 9068</c> §4. Matching follows the media-type rules of
        /// <c>RFC 7515</c> §4.1.9 (the <c>application/</c> prefix is optional and the comparison is case-insensitive),
        /// so <c>at+jwt</c>, <c>application/at+jwt</c>, and <c>AT+JWT</c> are treated as equal.
        /// </summary>
        /// <param name="validTokenTypes">The collection of allowable values for the <c>typ</c> header.</param>
        /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
        public ValidateJwtParameters ValidateTokenType(params IEnumerable<string> validTokenTypes)
        {
            // Normalize the allowable media types once at configuration time: the validator closure below runs
            // per-token, so a frozen set gives O(1) case-insensitive lookups and shields validation from later mutation.
            var validTokenTypeSet = validTokenTypes
                .Select(JoseTokenTypes.Normalize)
                .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

            return parameters.AddValidator(
                (context, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var header = context.DecodedJwt.Header;
                    if (
                        !header.TryGetPropertyValue<string>(
                            JoseClaimNames.Header.Typ,
                            out var tokenType
                        ) || string.IsNullOrEmpty(tokenType)
                    )
                        throw new TokenValidationException(
                            "The 'typ' header parameter is missing."
                        );

                    if (!validTokenTypeSet.Contains(JoseTokenTypes.Normalize(tokenType)))
                        throw new TokenValidationException(
                            $"The 'typ' header parameter '{tokenType}' is invalid because it does not match any of the expected token types."
                        );

                    return ValueTask.CompletedTask;
                }
            );
        }

        /// <summary>
        /// Adds a validator that asserts the <c>aud</c> claim exists in the JSON Web Token (JWT) payload with any of the
        /// specified <paramref name="validAudiences"/>.
        /// </summary>
        /// <param name="validAudiences">The collection of allowable values for the <c>aud></c> claim.</param>
        /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
        public ValidateJwtParameters ValidateAudience(params IEnumerable<string> validAudiences) =>
            parameters.ValidateClaim(
                JoseClaimNames.Payload.Aud,
                usePayload: true,
                allowCollection: true,
                validAudiences
            );

        /// <summary>
        /// Adds a validator, that when the signing key is a certificate, asserts the certificate is active and non-expired.
        /// </summary>
        /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
        public ValidateJwtParameters ValidateCertificateLifeTime()
        {
            // use 'long' vs 'DateTime/DateTimeOffset' comparisons to avoid overflow exceptions
            var clockSkewTicks = parameters.ClockSkew.Ticks;

            return parameters.AddValidator(
                (context, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (
                        context.SecretKey
                        is not AsymmetricSecretKey { HasCertificate: true } asymmetricSecretKey
                    )
                        return ValueTask.CompletedTask;

                    using var certificate = asymmetricSecretKey.ExportCertificate();
                    Debug.Assert(certificate is not null);

                    var nowTicks = context.TimeProvider.GetTimestampWithPrecisionInSeconds();

                    var notBefore = certificate.NotBefore.WithPrecisionInSeconds();
                    var notBeforeTicks = notBefore.Ticks;

                    var notAfter = certificate.NotAfter.WithPrecisionInSeconds();
                    var notAfterTicks = notAfter.Ticks;

                    if (notBeforeTicks > nowTicks + clockSkewTicks)
                        throw new TokenValidationException(
                            $"The certificate is not valid before {notBefore}."
                        );

                    if (notAfterTicks < nowTicks - clockSkewTicks)
                        throw new TokenValidationException(
                            $"The certificate is not valid after {notAfter}."
                        );

                    return ValueTask.CompletedTask;
                }
            );
        }

        /// <summary>
        /// Adds a validator that asserts the <c>nbf</c> and <c>exp</c> claims in the JSON Web Token (JWT) payload are valid.
        /// </summary>
        /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
        public ValidateJwtParameters ValidateTokenLifeTime() =>
            ValidateTokenLifeTime(parameters, _ => { });

        /// <summary>
        /// Adds a validator that asserts the <c>nbf</c> and <c>exp</c> claims in the JSON Web Token (JWT) payload are valid.
        /// </summary>
        /// <param name="configureOptions">A callback to configure validation options.</param>
        /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
        public ValidateJwtParameters ValidateTokenLifeTime(
            Action<ValidateTokenLifeTimeOptions> configureOptions
        )
        {
            var options = new ValidateTokenLifeTimeOptions();
            configureOptions(options);

            var clockSkewTicks = parameters.ClockSkew.Ticks;

            return parameters.AddValidator(
                (context, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var payload = context.DecodedJwt.Payload;
                    var hasNotBefore = payload.TryGetPropertyValue<DateTime>(
                        JoseClaimNames.Payload.Nbf,
                        out var notBefore
                    );
                    var hasExpires = payload.TryGetPropertyValue<DateTime>(
                        JoseClaimNames.Payload.Exp,
                        out var expires
                    );
                    var nowTicks = context.TimeProvider.GetTimestampWithPrecisionInSeconds();

                    if (!hasExpires && options.RequireExpirationTime)
                        throw new TokenValidationException(
                            "The token is missing an expiration time."
                        );

                    if (hasNotBefore && hasExpires && notBefore > expires)
                        throw new TokenValidationException(
                            $"The NotBefore value '{notBefore:O}' cannot be after the Expires value '{expires:O}'."
                        );

                    if (hasNotBefore && notBefore.Ticks > nowTicks + clockSkewTicks)
                        throw new TokenValidationException(
                            $"The token is not valid before '{notBefore:O}'."
                        );

                    if (hasExpires && expires.Ticks < nowTicks - clockSkewTicks)
                        throw new TokenValidationException(
                            $"The token is not valid after '{expires:O}'."
                        );

                    return ValueTask.CompletedTask;
                }
            );
        }
    }
}
