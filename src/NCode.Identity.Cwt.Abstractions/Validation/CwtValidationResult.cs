// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Cose;

namespace NCode.Identity.Cwt.Validation;

/// <summary>
/// The result of validating a CBOR Web Token (CWT): the claims and the verified message of a valid token, or a failure
/// code and reason. A failure reason never carries the token.
/// </summary>
// See ADR-1410.
[PublicAPI]
public sealed class CwtValidationResult
{
    private CwtValidationResult() { }

    /// <summary>
    /// Gets a value indicating whether the token is valid.
    /// </summary>
    [MemberNotNullWhen(false, nameof(FailureCode), nameof(FailureReason))]
    [MemberNotNullWhen(true, nameof(Claims), nameof(Message))]
    public bool IsValid { get; private init; }

    /// <summary>
    /// Gets the machine-readable code that names why the token is refused.
    /// </summary>
    public string? FailureCode { get; private init; }

    /// <summary>
    /// Gets the human-readable reason the token is refused.
    /// </summary>
    public string? FailureReason { get; private init; }

    /// <summary>
    /// Gets the claims of a valid token.
    /// </summary>
    public CwtClaimsSet? Claims { get; private init; }

    /// <summary>
    /// Gets the verified authenticated message of a valid token.
    /// </summary>
    public CoseVerifiedMessage? Message { get; private init; }

    /// <summary>
    /// Gets the decrypted message of a valid encrypted token, or <c>null</c> when the token is not encrypted.
    /// </summary>
    public CoseVerifiedMessage? Decryption { get; private init; }

    /// <summary>
    /// Creates the result of a valid token.
    /// </summary>
    /// <param name="claims">The claims of the token.</param>
    /// <param name="message">The verified authenticated message.</param>
    /// <param name="decryption">The decrypted message, or <c>null</c> when the token is not encrypted.</param>
    /// <returns>The result.</returns>
    public static CwtValidationResult Success(
        CwtClaimsSet claims,
        CoseVerifiedMessage message,
        CoseVerifiedMessage? decryption
    )
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(message);

        return new CwtValidationResult
        {
            IsValid = true,
            Claims = claims,
            Message = message,
            Decryption = decryption,
        };
    }

    /// <summary>
    /// Creates the result of a refused token.
    /// </summary>
    /// <param name="failureCode">The machine-readable code.</param>
    /// <param name="failureReason">The human-readable reason.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException"><paramref name="failureCode"/> or <paramref name="failureReason"/> is empty.</exception>
    public static CwtValidationResult Fail(string failureCode, string failureReason)
    {
        ArgumentException.ThrowIfNullOrEmpty(failureCode);
        ArgumentException.ThrowIfNullOrEmpty(failureReason);

        return new CwtValidationResult
        {
            IsValid = false,
            FailureCode = failureCode,
            FailureReason = failureReason,
        };
    }
}
