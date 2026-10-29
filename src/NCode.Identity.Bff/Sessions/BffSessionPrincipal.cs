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
using NCode.Identity.Jose;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Converts between the claims of a validated ID token, the claims stored in a session, and the session principal.
/// </summary>
internal static class BffSessionPrincipal
{
    private static readonly HashSet<string> DroppedClaimTypes = new(StringComparer.Ordinal)
    {
        JoseClaimNames.Payload.Nonce,
        JoseClaimNames.Payload.AtHash,
        JoseClaimNames.Payload.CHash,
        JoseClaimNames.Payload.SHash,
        JoseClaimNames.Payload.Exp,
        JoseClaimNames.Payload.Iat,
        JoseClaimNames.Payload.Nbf,
        JoseClaimNames.Payload.Aud,
        JoseClaimNames.Payload.Azp,
    };

    /// <summary>
    /// Selects the claims of an ID token that a session keeps, dropping the ones that only matter during validation.
    /// </summary>
    /// <param name="identity">The claims identity of the validated ID token.</param>
    public static IEnumerable<Claim> SelectSessionClaims(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return identity.Claims.Where(claim => !DroppedClaimTypes.Contains(claim.Type));
    }

    /// <summary>
    /// Merges the claims of a UserInfo response into the claims of a session: each UserInfo claim type replaces the
    /// session's claims of that type, except the types that anchor the session to the provider's authentication
    /// (<see cref="BffSessionClaimContext.ReservedClaimTypes"/>) and the ones that only matter during validation, which
    /// the UserInfo response cannot change.
    /// </summary>
    /// <param name="claims">The claims of the session, from the ID token.</param>
    /// <param name="userInfo">The claims of the UserInfo response.</param>
    // See ADR-0299.
    public static IReadOnlyList<Claim> MergeUserInfo(
        IEnumerable<Claim> claims,
        ClaimsIdentity userInfo
    )
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(userInfo);

        var replaced = userInfo.Claims.Where(IsMergeable).ToList();
        var replacedTypes = replaced.Select(claim => claim.Type).ToHashSet(StringComparer.Ordinal);

        return [.. claims.Where(claim => !replacedTypes.Contains(claim.Type)), .. replaced];
    }

    private static bool IsMergeable(Claim claim) =>
        !BffSessionClaimContext.ReservedClaimTypes.Contains(claim.Type)
        && !DroppedClaimTypes.Contains(claim.Type);

    /// <summary>
    /// Converts the claims of a session into the form the session stores: each claim's type, value and value type.
    /// </summary>
    /// <param name="claims">The claims of the session.</param>
    public static IReadOnlyList<BffStoredClaim> StoreClaims(IEnumerable<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        return
        [
            .. claims.Select(claim => new BffStoredClaim
            {
                Type = claim.Type,
                Value = claim.Value,
                ValueType = claim.ValueType,
            }),
        ];
    }

    /// <summary>
    /// Builds the principal of a session from its stored claims.
    /// </summary>
    /// <param name="claims">The stored claims.</param>
    /// <param name="issuer">The issuer that authenticated the user.</param>
    public static ClaimsPrincipal Create(IReadOnlyList<BffStoredClaim> claims, string issuer)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(issuer);

        var list = new List<Claim>(claims.Count);
        foreach (var stored in claims)
        {
            list.Add(new Claim(stored.Type, stored.Value, stored.ValueType, issuer));
        }

        var identity = new ClaimsIdentity(
            list,
            BffDefaults.AuthenticationScheme,
            JoseClaimNames.Payload.Name,
            JoseClaimNames.Payload.Role
        );
        return new ClaimsPrincipal(identity);
    }
}
