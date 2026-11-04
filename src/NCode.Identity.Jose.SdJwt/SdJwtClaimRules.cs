// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using NCode.Identity.Jose.SdJwt.Profiles;

namespace NCode.Identity.Jose.SdJwt;

/// <summary>
/// The claim-name rules of the selective disclosure mechanism itself: the names RFC 9901 reserves for the mechanism,
/// which are never disclosed and never present in the claims an issuer makes disclosable, and the top-level claims
/// the verifier judges from the issuer-signed payload before any disclosure is resolved, which are never selectively
/// disclosable under any profile.
/// </summary>
// See ADR-1405.
internal static class SdJwtClaimRules
{
    /// <summary>
    /// Gets the reserved claim names: <c>_sd</c>, <c>...</c> and <c>_sd_alg</c>.
    /// </summary>
    public static FrozenSet<string> ReservedClaimNames { get; } =
        new[]
        {
            JoseClaimNames.Payload.Sd,
            JoseClaimNames.Payload.SdArrayElement,
            JoseClaimNames.Payload.SdAlg,
        }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Gets the top-level claims that are never selectively disclosable, in whole or in part, whatever the profile:
    /// <c>iss</c>, <c>nbf</c> and <c>exp</c>, which the JWT validator judges on the issuer-signed payload, and
    /// <c>cnf</c>, which names the holder key before any disclosure is resolved.
    /// </summary>
    public static FrozenSet<string> ProtectedClaimNames { get; } =
        new[]
        {
            JoseClaimNames.Payload.Iss,
            JoseClaimNames.Payload.Nbf,
            JoseClaimNames.Payload.Exp,
            JoseClaimNames.Payload.Cnf,
        }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Determines whether a claim name is reserved by the selective disclosure mechanism.
    /// </summary>
    /// <param name="claimName">The claim name to check.</param>
    /// <returns><c>true</c> when the name is reserved; otherwise <c>false</c>.</returns>
    public static bool IsReserved(string claimName) => ReservedClaimNames.Contains(claimName);

    /// <summary>
    /// Determines whether a top-level claim is never selectively disclosable, by the format's own rule or by the
    /// profile's.
    /// </summary>
    /// <param name="claimName">The name of the top-level claim.</param>
    /// <param name="profile">The profile, or <c>null</c> for a plain SD-JWT.</param>
    /// <returns><c>true</c> when the claim is protected; otherwise <c>false</c>.</returns>
    public static bool IsProtected(string claimName, SdJwtProfile? profile) =>
        ProtectedClaimNames.Contains(claimName)
        || (profile is not null && profile.ProtectedClaims.Contains(claimName));

    /// <summary>
    /// Describes why a top-level claim is protected, for a refusal message.
    /// </summary>
    /// <param name="claimName">The name of a protected top-level claim.</param>
    /// <param name="profile">The profile, or <c>null</c> for a plain SD-JWT.</param>
    /// <returns>The reason, which names the claim and the profile when the profile protects it.</returns>
    public static string DescribeProtected(string claimName, SdJwtProfile? profile) =>
        ProtectedClaimNames.Contains(claimName) || profile is null
            ? $"The '{claimName}' claim is never selectively disclosable, in whole or in part."
            : $"The '{claimName}' claim is never selectively disclosable, in whole or in part, under the '{profile.Name}' profile.";
}
