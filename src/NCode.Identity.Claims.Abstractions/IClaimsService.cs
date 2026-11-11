// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;

namespace NCode.Identity.Claims;

/// <summary>
/// Provides an abstraction for working with claims
/// </summary>
[PublicAPI]
public interface IClaimsService
{
    /// <summary>
    /// Copies claims from a <see cref="ClaimsPrincipal"/> to a new collection
    /// but only the specified claim types and optionally preventing any duplicates.
    /// </summary>
    /// <param name="source">The source <see cref="ClaimsPrincipal"/> to copy claims from.</param>
    /// <param name="target">The target collection to copy claims to.</param>
    /// <param name="preventDuplicates">Indicates if duplicates claims types should be prevented in the <paramref name="target"/>.</param>
    /// <param name="claimTypes">The claim types to copy.</param>
    void CopyClaims(
        ClaimsPrincipal source,
        ICollection<Claim> target,
        bool preventDuplicates,
        params IEnumerable<string> claimTypes
    );
}
