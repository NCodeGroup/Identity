// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides issuance and validation of server-provided DPoP nonces
/// (<see href="https://datatracker.ietf.org/doc/html/rfc9449#section-8">RFC 9449 §8</see>). A nonce lets the server
/// contribute entropy to a DPoP proof. The default implementation records each nonce, scoped to its tenant, in the shared
/// <c>IExpiringKeySet</c>, so a nonce issued by one replica is recognized by every replica that shares the set.
/// </summary>
[PublicAPI]
public interface IDPoPNonceService
{
    /// <summary>
    /// Issues a fresh nonce the client must echo in its next DPoP proof.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The issued nonce value.</returns>
    ValueTask<string> IssueNonceAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates a nonce presented in a DPoP proof.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="nonce">The nonce value carried by the DPoP proof.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> when the nonce is recognized and still fresh; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> ValidateNonceAsync(
        OpenIdContext openIdContext,
        string nonce,
        CancellationToken cancellationToken
    );
}
