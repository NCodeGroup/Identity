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
