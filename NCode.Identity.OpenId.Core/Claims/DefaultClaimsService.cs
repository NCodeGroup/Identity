#region Copyright Preamble
// Copyright @ 2025 NCode Group
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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Claims;

/// <summary>
/// Provides a default implementation of the <see cref="IClaimsService"/> abstraction.
/// </summary>
[PublicAPI]
public class DefaultClaimsService : IClaimsService
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
