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
/// A requirement that asserts the <c>nbf</c>, <c>iat</c>, and <c>exp</c> claims in the JSON Web Token (JWT) payload
/// are valid, allowing for the configured <see cref="ClockSkew"/>: the token is not yet expired, is not used before
/// its <c>nbf</c>, was not issued in the future, and (when <see cref="MaxLifetime"/> is set) is not valid for longer
/// than the maximum.
/// </summary>
/// <param name="RequireExpirationTime"><c>true</c> if the <c>exp</c> claim is required.</param>
/// <param name="ClockSkew">The amount of time to allow for clock skew when comparing lifetime claims.</param>
[PublicAPI]
public sealed record TokenLifetimeRequirement(bool RequireExpirationTime, TimeSpan ClockSkew)
    : IJwtValidationRequirement
{
    /// <summary>
    /// Gets the longest lifetime the token may have, or <see langword="null"/> for no maximum. The lifetime runs from
    /// the earlier of the token's <c>iat</c> and <c>nbf</c> to its <c>exp</c>; a token that asserts neither is measured
    /// from the validation time plus <see cref="ClockSkew"/>, so the remaining validity is always bounded. When set, an
    /// <c>exp</c> claim is required regardless of <see cref="RequireExpirationTime"/>, since a token without one has
    /// an unbounded lifetime.
    /// </summary>
    public TimeSpan? MaxLifetime { get; init; }
}
