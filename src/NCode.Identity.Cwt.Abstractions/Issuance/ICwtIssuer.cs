// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cwt.Issuance;

/// <summary>
/// Issues CBOR Web Tokens (CWT, RFC 8392): a claims set protected by a <c>COSE_Sign1</c> or <c>COSE_Mac0</c> message,
/// optionally inside a <c>COSE_Encrypt0</c> or <c>COSE_Encrypt</c> message.
/// </summary>
// See ADR-1410.
[PublicAPI]
public interface ICwtIssuer
{
    /// <summary>
    /// Issues a CWT.
    /// </summary>
    /// <param name="parameters">The claims and the protection of the token.</param>
    /// <param name="cancellationToken">A token to cancel the signing.</param>
    /// <returns>The encoded token.</returns>
    /// <exception cref="ArgumentException">A parameter is invalid, such as an additional claim that the typed members
    /// own, or an expiration time that is not after the start of the token.</exception>
    ValueTask<byte[]> IssueAsync(
        CwtIssuanceParameters parameters,
        CancellationToken cancellationToken
    );
}
