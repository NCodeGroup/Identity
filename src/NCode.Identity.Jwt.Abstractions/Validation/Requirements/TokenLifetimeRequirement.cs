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
/// A requirement that asserts the <c>nbf</c> and <c>exp</c> claims in the JSON Web Token (JWT) payload are valid,
/// allowing for the configured <see cref="ClockSkew"/>.
/// </summary>
/// <param name="RequireExpirationTime"><c>true</c> if the <c>exp</c> claim is required.</param>
/// <param name="ClockSkew">The amount of time to allow for clock skew when comparing lifetime claims.</param>
[PublicAPI]
public sealed record TokenLifetimeRequirement(bool RequireExpirationTime, TimeSpan ClockSkew)
    : IJwtValidationRequirement;
