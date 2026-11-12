// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Jose;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Converts between the claims of a validated ID token, the claims stored in a session, and the session principal.
/// </summary>
internal static class BffSessionPrincipal
{
    private static HashSet<string> DroppedClaimTypes { get; } =
        new(StringComparer.Ordinal)
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
            BffClaimTypes.LogoutUrl,
            BffClaimTypes.LogoutSid,
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
    // See ADR-1301.
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

    /// <summary>
    /// Builds the principal of a session from its state, with the claims that carry the action and the guard of the
    /// session's logout form.
    /// </summary>
    /// <param name="state">The state of the session.</param>
    /// <param name="logoutUrl">The local URL of the frontend's logout endpoint.</param>
    public static ClaimsPrincipal Create(BffSessionState state, string logoutUrl)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(logoutUrl);

        var principal = Create(state.Payload.Claims, state.Metadata.Issuer);
        var identity = principal.Identities.Single();
        identity.AddClaim(new Claim(BffClaimTypes.LogoutUrl, logoutUrl));
        identity.AddClaim(new Claim(BffClaimTypes.LogoutSid, state.Payload.LogoutGuard));
        return principal;
    }
}
