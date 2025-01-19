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
using NCode.Identity.Jose;

namespace NCode.Identity.OpenId.Logic;

/// <summary>
/// Represents a delegate that extracts a <see cref="ClaimsIdentity"/> from a <see cref="ClaimsPrincipal"/>.
/// </summary>
/// <param name="subject">The <see cref="ClaimsPrincipal"/> to extract the <see cref="ClaimsIdentity"/> from.</param>
/// <returns>The <see cref="ClaimsIdentity"/> from the <see cref="ClaimsPrincipal"/>.</returns>
public delegate ClaimsIdentity GetSubjectIdentityDelegate(ClaimsPrincipal subject);

/// <summary>
/// Represents a delegate that extracts the subject id from a <see cref="ClaimsPrincipal"/>.
/// </summary>
/// <param name="subject">The <see cref="ClaimsPrincipal"/> to search for the subject id.</param>
/// <returns>The subject id if found; otherwise <c>null</c>.</returns>
public delegate string? GetSubjectIdDelegate(ClaimsPrincipal subject);

/// <summary>
/// Provides default implementations for various operations related to <see cref="ClaimsPrincipal"/>.
/// </summary>
public static class DefaultClaimsPrincipalLogic
{
    /// <summary>
    /// Returns the default implementation of <see cref="GetSubjectIdentityDelegate"/> that extracts the primary <see cref="ClaimsIdentity"/> from a <see cref="ClaimsPrincipal"/>.
    /// </summary>
    /// <param name="subject">The <see cref="ClaimsPrincipal"/> to extract the <see cref="ClaimsIdentity"/> from.</param>
    /// <returns>The <see cref="ClaimsIdentity"/> from the <see cref="ClaimsPrincipal"/>.</returns>
    public static ClaimsIdentity GetSubjectIdentity(this ClaimsPrincipal subject) =>
        subject.Identity as ClaimsIdentity ?? subject.Identities.First();

    /// <summary>
    /// Returns the default implementation of a delegate that extracts the subject id from a <see cref="ClaimsPrincipal"/>.
    /// This implementation will search for the <see cref="JoseClaimNames.Payload.Sub"/> claim first,
    /// then the <see cref="ClaimTypes.NameIdentifier"/> claim (if allowed),
    /// and finally the <see cref="ClaimTypes.Upn"/> claim (if allowed).
    /// The <see cref="JoseClaimNames.Payload.Sub"/> claim will always take precedence over the other claims.
    /// </summary>
    /// <param name="allowNameId">Whether to allow the <see cref="ClaimTypes.NameIdentifier"/> claim to be used as the subject id.</param>
    /// <param name="allowUpn">Whether to allow the <see cref="ClaimTypes.Upn"/> claim to be used as the subject id.</param>
    public static GetSubjectIdDelegate GetSubjectId(bool allowNameId, bool allowUpn) => subject =>
    {
        string? nameId = null;
        string? upn = null;

        foreach (var claim in subject.Claims.Where(claim => !string.IsNullOrEmpty(claim.Value)))
        {
            switch (claim.Type)
            {
                case JoseClaimNames.Payload.Sub:
                    return claim.Value;

                case ClaimTypes.NameIdentifier when allowNameId:
                    nameId = claim.Value;
                    break;

                case ClaimTypes.Upn when allowUpn:
                    upn = claim.Value;
                    break;
            }
        }

        return nameId ?? upn;
    };
}
