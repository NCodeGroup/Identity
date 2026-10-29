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

using System.Security.Claims;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Claims;

/// <summary>
/// Maps a JSON claim set — the payload of a JWT, or another claim set such as an OpenID Connect UserInfo response — to a
/// <see cref="ClaimsIdentity"/>, typing each claim by its JSON value, so every claim set the family reads maps the same
/// way. A member whose value is an array becomes one claim per element.
/// </summary>
// See ADR-0122.
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
