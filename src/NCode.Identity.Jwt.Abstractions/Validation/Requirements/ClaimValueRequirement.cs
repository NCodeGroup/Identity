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
