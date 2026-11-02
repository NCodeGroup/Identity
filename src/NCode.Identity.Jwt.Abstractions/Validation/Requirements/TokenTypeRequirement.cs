// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
