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
using NCode.Identity.Jose;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Provides fluent extension methods for configuring <see cref="JwtValidationParameters"/>. Each method only
/// constructs a declarative <see cref="IJwtValidationRequirement"/> (or sets a data property) and appends it to
/// <see cref="JwtValidationParameters.Requirements"/>; the validation logic lives in the registered
/// <see cref="IJwtValidationHandler"/> strategies, so these helpers carry no logic and remain in the Abstractions package.
/// </summary>
[PublicAPI]
public static class JwtValidationParametersExtensions
{
    /// <param name="parameters">The <see cref="JwtValidationParameters"/> instance.</param>
    extension(JwtValidationParameters parameters)
    {
        /// <summary>
        /// Sets <see cref="JwtValidationParameters.ValidationKeys"/> to the specified <paramref name="secretKeys"/>.
        /// </summary>
        /// <param name="secretKeys">The collection of <see cref="SecretKey"/> instances to use for JSON Web Token (JWT) validation.</param>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters UseValidationKeys(IEnumerable<SecretKey> secretKeys)
        {
            parameters.ValidationKeys =
                secretKeys as IReadOnlyCollection<SecretKey> ?? [.. secretKeys];
            return parameters;
        }

        /// <summary>
        /// Sets <see cref="JwtValidationParameters.DecryptionKeys"/> to the specified <paramref name="secretKeys"/>.
        /// </summary>
        /// <param name="secretKeys">The collection of <see cref="SecretKey"/> instances that decrypt the enclosing JWE
        /// of a Nested JSON Web Token (JWT).</param>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters UseDecryptionKeys(IEnumerable<SecretKey> secretKeys)
        {
            parameters.DecryptionKeys =
                secretKeys as IReadOnlyCollection<SecretKey> ?? [.. secretKeys];
            return parameters;
        }

        /// <summary>
        /// Adds the specified <paramref name="requirement"/> to <see cref="JwtValidationParameters.Requirements"/>.
        /// </summary>
        /// <param name="requirement">The <see cref="IJwtValidationRequirement"/> to add.</param>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters AddRequirement(IJwtValidationRequirement requirement)
        {
            parameters.Requirements.Add(requirement);
            return parameters;
        }

        /// <summary>
        /// Adds an <see cref="AssertionRequirement"/> that validates the token using the specified <paramref name="assertion"/>.
        /// </summary>
        /// <param name="assertion">The delegate that returns <c>true</c> when the token satisfies the requirement.</param>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters RequireAssertion(
            Func<JwtValidationContext, CancellationToken, ValueTask<bool>> assertion
        ) => parameters.AddRequirement(new AssertionRequirement(assertion));

        /// <summary>
        /// Adds a <see cref="ClaimValueRequirement"/> that asserts the specified <paramref name="claimName"/> exists
        /// with any value from <paramref name="validValues"/>.
        /// </summary>
        /// <param name="claimName">The name of the claim to validate.</param>
        /// <param name="usePayload"><c>true</c> to check the payload claims, <c>false</c> to check the header claims.</param>
        /// <param name="allowCollection"><c>true</c> if the claim allows multiple values; <c>false</c> if the claim must be a single value.</param>
        /// <param name="validValues">The collection of allowable values for the claim.</param>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters ValidateClaim(
            string claimName,
            bool usePayload,
            bool allowCollection,
            IEnumerable<string> validValues
        ) =>
            parameters.AddRequirement(
                new ClaimValueRequirement(
                    claimName,
                    usePayload,
                    allowCollection,
                    validValues as IReadOnlyCollection<string> ?? [.. validValues]
                )
            );

        /// <summary>
        /// Adds a requirement that asserts the <c>iss</c> payload claim is any of the specified <paramref name="validIssuers"/>.
        /// The requirement fails with <see cref="JwtValidationFailureCodes.InvalidIssuer"/>.
        /// </summary>
        /// <param name="validIssuers">The collection of allowable values for the <c>iss</c> claim.</param>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters ValidateIssuer(params IEnumerable<string> validIssuers) =>
            parameters.AddRequirement(
                new ClaimValueRequirement(
                    JoseClaimNames.Payload.Iss,
                    UsePayload: true,
                    AllowCollection: false,
                    validIssuers as IReadOnlyCollection<string> ?? [.. validIssuers]
                )
                {
                    FailureCode = JwtValidationFailureCodes.InvalidIssuer,
                }
            );

        /// <summary>
        /// Adds a requirement that asserts the <c>aud</c> payload claim is any of the specified <paramref name="validAudiences"/>.
        /// The requirement fails with <see cref="JwtValidationFailureCodes.InvalidAudience"/>.
        /// </summary>
        /// <param name="validAudiences">The collection of allowable values for the <c>aud</c> claim.</param>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters ValidateAudience(
            params IEnumerable<string> validAudiences
        ) =>
            parameters.AddRequirement(
                new ClaimValueRequirement(
                    JoseClaimNames.Payload.Aud,
                    UsePayload: true,
                    AllowCollection: true,
                    validAudiences as IReadOnlyCollection<string> ?? [.. validAudiences]
                )
                {
                    FailureCode = JwtValidationFailureCodes.InvalidAudience,
                }
            );

        /// <summary>
        /// Adds a <see cref="TokenTypeRequirement"/> that asserts the <c>typ</c> JOSE header matches any of the
        /// specified <paramref name="validTokenTypes"/>.
        /// </summary>
        /// <param name="validTokenTypes">The collection of allowable values for the <c>typ</c> header.</param>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters ValidateTokenType(
            params IEnumerable<string> validTokenTypes
        ) =>
            parameters.AddRequirement(
                new TokenTypeRequirement(
                    validTokenTypes as IReadOnlyCollection<string> ?? [.. validTokenTypes]
                )
            );

        /// <summary>
        /// Adds a <see cref="CertificateLifetimeRequirement"/> that, when the signing key is a certificate, asserts the
        /// certificate is active and non-expired (capturing the current <see cref="JwtValidationParameters.ClockSkew"/>).
        /// </summary>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters ValidateCertificateLifeTime() =>
            parameters.AddRequirement(new CertificateLifetimeRequirement(parameters.ClockSkew));

        /// <summary>
        /// Adds a <see cref="TokenLifetimeRequirement"/> that asserts the <c>nbf</c>, <c>iat</c>, and <c>exp</c> claims
        /// are valid (capturing the current <see cref="JwtValidationParameters.ClockSkew"/>).
        /// </summary>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters ValidateTokenLifeTime() =>
            parameters.ValidateTokenLifeTime(static _ => { });

        /// <summary>
        /// Adds a <see cref="TokenLifetimeRequirement"/> that asserts the <c>nbf</c>, <c>iat</c>, and <c>exp</c> claims
        /// are valid (capturing the current <see cref="JwtValidationParameters.ClockSkew"/>).
        /// </summary>
        /// <param name="configureOptions">A callback to configure validation options.</param>
        /// <returns>The <see cref="JwtValidationParameters"/> instance for method chaining.</returns>
        public JwtValidationParameters ValidateTokenLifeTime(
            Action<ValidateTokenLifeTimeOptions> configureOptions
        )
        {
            var options = new ValidateTokenLifeTimeOptions();
            configureOptions(options);
            return parameters.AddRequirement(
                new TokenLifetimeRequirement(options.RequireExpirationTime, parameters.ClockSkew)
                {
                    MaxLifetime = options.MaxLifetime,
                    RequireNotBefore = options.RequireNotBefore,
                    RequireIssuedAt = options.RequireIssuedAt,
                }
            );
        }
    }
}
