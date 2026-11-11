// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.Claims;

/// <summary>
/// Provides a default implementation of the <see cref="IClaimsService"/> abstraction.
/// </summary>
internal class DefaultClaimsService : IClaimsService
{
    /// <inheritdoc />
    public void CopyClaims(
        ClaimsPrincipal source,
        ICollection<Claim> target,
        bool preventDuplicates,
        params IEnumerable<string> claimTypes
    )
    {
        var claims = source.Claims.Where(claim => claimTypes.Contains(claim.Type));
        foreach (var claim in claims)
        {
            if (preventDuplicates && target.Any(other => other.Type == claim.Type))
            {
                continue;
            }

            target.Add(claim);
        }
    }
}
