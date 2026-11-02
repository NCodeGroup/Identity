// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Claims;

/// <summary>
/// Maps a JSON claim set — the payload of a JWT, or another claim set such as an OpenID Connect UserInfo response — to a
/// <see cref="ClaimsIdentity"/>, typing each claim by its JSON value, so every claim set the family reads maps the same
/// way. A member whose value is an array becomes one claim per element.
/// </summary>
// See ADR-1122.
[PublicAPI]
public interface IClaimsIdentityFactory
{
    /// <summary>
    /// Creates a <see cref="ClaimsIdentity"/> from a JSON claim set.
    /// </summary>
    /// <param name="claimSet">The JSON object whose members are the claims.</param>
    /// <param name="authenticationType">The authentication type of the identity.</param>
    /// <param name="nameClaimType">The claim type of the identity's name, or <see langword="null"/> for the default.</param>
    /// <param name="roleClaimType">The claim type of the identity's roles, or <see langword="null"/> for the
    /// default.</param>
    /// <param name="issuer">The issuer of the claims when the claim set carries no <c>iss</c>, or
    /// <see langword="null"/> for <see cref="ClaimsIdentity.DefaultIssuer"/>.</param>
    /// <returns>The identity.</returns>
    /// <exception cref="ArgumentException"><paramref name="claimSet"/> is not a JSON object.</exception>
    ClaimsIdentity CreateClaimsIdentity(
        JsonElement claimSet,
        string authenticationType,
        string? nameClaimType = null,
        string? roleClaimType = null,
        string? issuer = null
    );
}
