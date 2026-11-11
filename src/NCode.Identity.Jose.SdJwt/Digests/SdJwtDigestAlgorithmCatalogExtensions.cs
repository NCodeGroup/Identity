// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Jose.SdJwt.Verification;
using NCode.Identity.Json;

namespace NCode.Identity.Jose.SdJwt.Digests;

/// <summary>
/// Provides extension methods for <see cref="ISdJwtDigestAlgorithmCatalog"/> that resolve the hash algorithm an SD-JWT
/// names, so the holder and the verifier read the <c>_sd_alg</c> claim under one rule.
/// </summary>
internal static class SdJwtDigestAlgorithmCatalogExtensions
{
    extension(ISdJwtDigestAlgorithmCatalog catalog)
    {
        /// <summary>
        /// Resolves the hash algorithm that the <c>_sd_alg</c> claim of an issuer-signed JWT payload names, or
        /// <c>sha-256</c> when the claim is absent (RFC 9901 §4.1.1).
        /// </summary>
        /// <param name="payload">The issuer-signed JWT payload, a JSON object.</param>
        /// <returns>The registered algorithm.</returns>
        /// <exception cref="SdJwtException">The claim is present but not a non-empty string, or names an algorithm that
        /// is not registered; its <see cref="SdJwtException.FailureCode"/> is
        /// <see cref="SdJwtVerificationFailureCodes.UnsupportedDigestAlgorithm"/>.</exception>
        public SdJwtDigestAlgorithm Resolve(JsonElement payload)
        {
            ArgumentNullException.ThrowIfNull(catalog);

            // An absent claim defaults; a present claim that is not a non-empty string fails closed.
            var name = SdJwtDigestAlgorithms.DefaultName;
            if (
                payload.TryGetProperty(JoseClaimNames.Payload.SdAlg, out var sdAlg)
                && (!sdAlg.TryGetValue(out name) || string.IsNullOrEmpty(name))
            )
                throw new SdJwtException(
                    SdJwtVerificationFailureCodes.UnsupportedDigestAlgorithm,
                    "The '_sd_alg' claim must be a non-empty string."
                );

            return catalog.TryGet(name, out var algorithm)
                ? algorithm
                : throw new SdJwtException(
                    SdJwtVerificationFailureCodes.UnsupportedDigestAlgorithm,
                    $"The digest algorithm '{name}' is not registered."
                );
        }
    }
}
