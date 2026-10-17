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
/// A requirement that asserts the JSON Web Token (JWT) carries an explicit <c>typ</c> JOSE header whose media type
/// matches any of <see cref="ValidTokenTypes"/>. This is the explicit-typing defense against token-substitution /
/// cross-JWT-confusion attacks recommended by <c>RFC 8725</c> §3.11 and mandated for JWT access tokens by
/// <c>RFC 9068</c> §4. Matching follows the media-type rules of <c>RFC 7515</c> §4.1.9 (the <c>application/</c>
/// prefix is optional and the comparison is case-insensitive).
/// </summary>
/// <param name="ValidTokenTypes">The collection of allowable values for the <c>typ</c> header.</param>
[PublicAPI]
public sealed record TokenTypeRequirement(IReadOnlyCollection<string> ValidTokenTypes)
    : IJwtValidationRequirement;
