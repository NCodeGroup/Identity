// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cwt.Validation;

/// <summary>
/// Validates CBOR Web Tokens (CWT, RFC 8392) fail-closed: the algorithms, the keys, the issuer, the audience, the
/// lifetime and the type of the token are all named by the caller, and no claim is read before the COSE layer of the
/// token verifies.
/// </summary>
// See ADR-1410.
[PublicAPI]
public interface ICwtValidator
{
    /// <summary>
    /// Validates a token.
    /// </summary>
    /// <param name="token">The encoded token, with or without the CWT tag.</param>
    /// <param name="parameters">What the validator accepts.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the validation, which carries a failure code and reason and never the token.</returns>
    /// <exception cref="ArgumentException">A parameter is missing or invalid; the token is not read.</exception>
    ValueTask<CwtValidationResult> ValidateAsync(
        ReadOnlyMemory<byte> token,
        CwtValidationParameters parameters,
        CancellationToken cancellationToken
    );
}
