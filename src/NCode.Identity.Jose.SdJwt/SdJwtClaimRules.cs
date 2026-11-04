// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;

namespace NCode.Identity.Jose.SdJwt;

/// <summary>
/// The claim names that RFC 9901 reserves for the selective disclosure mechanism itself, which are never disclosed and
/// never present in the claims an issuer makes disclosable.
/// </summary>
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
    /// Determines whether a claim name is reserved by the selective disclosure mechanism.
    /// </summary>
    /// <param name="claimName">The claim name to check.</param>
    /// <returns><c>true</c> when the name is reserved; otherwise <c>false</c>.</returns>
    public static bool IsReserved(string claimName) => ReservedClaimNames.Contains(claimName);
}
