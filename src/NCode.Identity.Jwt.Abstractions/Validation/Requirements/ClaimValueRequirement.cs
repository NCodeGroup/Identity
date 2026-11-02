// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// A requirement that asserts a claim exists in the JSON Web Token (JWT) with any value from <see cref="ValidValues"/>.
/// </summary>
/// <param name="ClaimName">The name of the claim to validate.</param>
/// <param name="UsePayload"><c>true</c> to check the payload claims; <c>false</c> to check the header claims.</param>
/// <param name="AllowCollection"><c>true</c> if the claim allows multiple values; <c>false</c> if it must be a single value.</param>
/// <param name="ValidValues">The collection of allowable values for the claim.</param>
[PublicAPI]
public sealed record ClaimValueRequirement(
    string ClaimName,
    bool UsePayload,
    bool AllowCollection,
    IReadOnlyCollection<string> ValidValues
) : IJwtValidationRequirement
{
    /// <summary>
    /// Gets the machine-readable code the requirement fails with. The default is
    /// <see cref="JwtValidationFailureCodes.InvalidClaim"/>.
    /// </summary>
    public string FailureCode { get; init; } = JwtValidationFailureCodes.InvalidClaim;
}
