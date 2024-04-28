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

using System.Diagnostics;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Json;
using NCode.Identity.JsonWebTokens.Exceptions;
using NCode.Identity.JsonWebTokens.Options;
using NCode.Identity.Secrets;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides the ability to configure various Json Web Token (JWT) validators.
/// </summary>
public static class Validators
{
    /// <summary>
    /// Adds a validator that asserts the <c>iss</c> claim exists in the Json Web Token (JWT) payload with any of the
    /// specified <paramref name="validIssuers"/>.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <param name="validIssuers">The collection of allowable values for the <c>iss></c> claim.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters ValidateIssuer(
        this ValidateJwtParameters parameters,
        params string[] validIssuers) =>
        parameters.ValidateClaim(
            JoseClaimNames.Payload.Iss,
            usePayload: true,
            allowCollection: false,
            validIssuers);

    /// <summary>
    /// Adds a validator that asserts the <c>aud</c> claim exists in the Json Web Token (JWT) payload with any of the
    /// specified <paramref name="validAudiences"/>.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <param name="validAudiences">The collection of allowable values for the <c>aud></c> claim.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters ValidateAudience(
        this ValidateJwtParameters parameters,
        params string[] validAudiences) =>
        parameters.ValidateClaim(
            JoseClaimNames.Payload.Aud,
            usePayload: true,
            allowCollection: true,
            validAudiences);

    /// <summary>
    /// Adds a validator, that when the signing key is a certificate, asserts the certificate is active and non-expired.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters ValidateCertificateLifeTime(this ValidateJwtParameters parameters)
    {
        // use 'long' vs 'DateTime/DateTimeOffset' comparisons to avoid overflow exceptions
        var clockSkewTicks = parameters.ClockSkew.Ticks;

        return parameters.AddValidator((context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.SecretKey is not AsymmetricSecretKey { HasCertificate: true } asymmetricSecretKey)
                return ValueTask.CompletedTask;

            using var certificate = asymmetricSecretKey.ExportCertificate();
            Debug.Assert(certificate is not null);

            var nowTicks = context.SystemClock.UtcNow.UtcTicks;

            var notBefore = certificate.NotBefore.ToUniversalTime();
            var notBeforeTicks = notBefore.Ticks;

            var notAfter = certificate.NotAfter.ToUniversalTime();
            var notAfterTicks = notAfter.Ticks;

            if (notBeforeTicks > nowTicks + clockSkewTicks)
                throw new TokenValidationException($"The certificate is not valid before {notBefore}.");

            if (notAfterTicks < nowTicks - clockSkewTicks)
                throw new TokenValidationException($"The certificate is not valid after {notAfter}.");

            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// Adds a validator that asserts the <c>nbf</c> and <c>exp</c> claims in the Json Web Token (JWT) payload are valid.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters ValidateTokenLifeTime(this ValidateJwtParameters parameters) =>
        ValidateTokenLifeTime(parameters, _ => { });

    /// <summary>
    /// Adds a validator that asserts the <c>nbf</c> and <c>exp</c> claims in the Json Web Token (JWT) payload are valid.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <param name="configureOptions">A callback to configure validation options.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters ValidateTokenLifeTime(
        this ValidateJwtParameters parameters,
        Action<ValidateTokenLifeTimeOptions> configureOptions)
    {
        var options = new ValidateTokenLifeTimeOptions();
        configureOptions(options);

        var clockSkewTicks = parameters.ClockSkew.Ticks;

        return parameters.AddValidator((context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payload = context.DecodedJwt.Payload;
            var hasNotBefore = payload.TryGetPropertyValue<DateTime>(JoseClaimNames.Payload.Nbf, out var notBefore);
            var hasExpires = payload.TryGetPropertyValue<DateTime>(JoseClaimNames.Payload.Exp, out var expires);
            var nowTicks = context.SystemClock.UtcNow.UtcTicks;

            if (!hasExpires && options.RequireExpirationTime)
                throw new TokenValidationException("The token is missing an expiration time.");

            if (hasNotBefore && hasExpires && notBefore > expires)
                throw new TokenValidationException($"The NotBefore value '{notBefore:O}' cannot be after the Expires value '{expires:O}'.");

            if (hasNotBefore && notBefore.Ticks > nowTicks + clockSkewTicks)
                throw new TokenValidationException($"The token is not valid before '{notBefore:O}'.");

            if (hasExpires && expires.Ticks < nowTicks - clockSkewTicks)
                throw new TokenValidationException($"The token is not valid after '{expires:O}'.");

            return ValueTask.CompletedTask;
        });
    }
}
