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
using NCode.Identity.JsonWebTokens;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Shared helper for the default subject-metadata token projection handlers: emits a metadata bag as a single nested
/// JSON-object claim under a reserved claim type (ADR-0055).
/// </summary>
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
