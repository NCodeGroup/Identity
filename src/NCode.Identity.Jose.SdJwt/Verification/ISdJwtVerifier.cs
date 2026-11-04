// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// Verifies presentations of Selective Disclosure JWTs (RFC 9901 §7): the issuer-signed JWT under the caller's JWT
/// validation policy, the disclosures against the digests they must match, and the Key Binding JWT against the holder
/// key in <c>cnf</c> and the audience and nonce the verifier supplied.
/// </summary>
[PublicAPI]
public interface ISdJwtVerifier
{
    /// <summary>
    /// Verifies a presentation.
    /// </summary>
    /// <param name="presentation">The presentation in the combined format.</param>
    /// <param name="parameters">The policy to verify against.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The disclosed claims, or the code of the rule the presentation failed.</returns>
    /// <exception cref="ArgumentException">The parameters name no signature algorithms for the issuer-signed JWT.</exception>
    ValueTask<SdJwtVerificationResult> VerifyAsync(
        string presentation,
        SdJwtVerificationParameters parameters,
        CancellationToken cancellationToken
    );
}
