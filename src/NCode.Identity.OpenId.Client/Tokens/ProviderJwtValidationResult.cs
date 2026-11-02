// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Validation;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// The outcome of <see cref="ProviderJwtValidator.ValidateAsync"/>: the validated token, or the reason it is invalid.
/// </summary>
internal readonly struct ProviderJwtValidationResult
{
    private ProviderJwtValidationResult(
        JwtValidationResult? result,
        DecodedJwt? decodedJwt,
        string? failureCode,
        string? failureReason
    )
    {
        Result = result;
        DecodedJwt = decodedJwt;
        FailureCode = failureCode;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets the result of the JWT validation when the token is valid.
    /// </summary>
    public JwtValidationResult? Result { get; }

    /// <summary>
    /// Gets the decoded token when it is valid.
    /// </summary>
    public DecodedJwt? DecodedJwt { get; }

    /// <summary>
    /// Gets the machine-readable code of the reason the token is invalid, a <see cref="TokenValidationFailureCodes"/>
    /// value.
    /// </summary>
    public string? FailureCode { get; }

    /// <summary>
    /// Gets the reason the token is invalid.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// Gets a value indicating whether the token is valid.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Result), nameof(DecodedJwt))]
    [MemberNotNullWhen(false, nameof(FailureCode), nameof(FailureReason))]
    public bool IsValid => Result is not null;

    /// <summary>
    /// Creates the result of a valid token.
    /// </summary>
    public static ProviderJwtValidationResult Success(JwtValidationResult result) =>
        new(
            result,
            result.DecodedJwt
                ?? throw new ArgumentException("The JWT validation failed.", nameof(result)),
            null,
            null
        );

    /// <summary>
    /// Creates the result of an invalid token.
    /// </summary>
    public static ProviderJwtValidationResult Fail(string failureCode, string failureReason) =>
        new(null, null, failureCode, failureReason);
}
