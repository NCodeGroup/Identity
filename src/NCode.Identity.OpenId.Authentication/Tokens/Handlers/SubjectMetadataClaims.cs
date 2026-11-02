// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using NCode.Identity.Jwt.Claims;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Shared helper for the default subject-metadata token projection handlers: emits a metadata bag as a single nested
/// JSON-object claim under a reserved claim type.
/// </summary>
// See ADR-1055.
internal static class SubjectMetadataClaims
{
    /// <summary>
    /// Adds <paramref name="bag"/> to <paramref name="claims"/> as one <see cref="JsonClaimValueTypes.Json"/> claim
    /// under <paramref name="claimType"/>, skipping an absent/empty bag and never duplicating a claim type another
    /// enricher already supplied.
    /// </summary>
    public static void AddMetadataClaim(
        ICollection<Claim> claims,
        string claimType,
        JsonElement bag
    )
    {
        // Skip an absent or empty bag so the token never carries an empty-object claim.
        if (bag.ValueKind != JsonValueKind.Object || !bag.EnumerateObject().MoveNext())
        {
            return;
        }

        foreach (var existing in claims)
        {
            if (string.Equals(existing.Type, claimType, StringComparison.Ordinal))
            {
                return;
            }
        }

        claims.Add(new Claim(claimType, bag.GetRawText(), JsonClaimValueTypes.Json));
    }
}
