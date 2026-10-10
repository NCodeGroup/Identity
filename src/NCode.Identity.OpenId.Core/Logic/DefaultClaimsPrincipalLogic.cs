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

namespace NCode.Identity.OpenId.Logic;

/// <summary>
/// Provides default implementations for various operations related to <see cref="ClaimsPrincipal"/>.
/// </summary>
internal static class DefaultClaimsPrincipalLogic
{
    extension(ClaimsPrincipal subject)
    {
        /// <summary>
        /// Returns the default implementation of <see cref="GetSubjectIdentityDelegate"/> that extracts the primary <see cref="ClaimsIdentity"/> from a <see cref="ClaimsPrincipal"/>.
        /// </summary>
        /// <returns>The <see cref="ClaimsIdentity"/> from the <see cref="ClaimsPrincipal"/>.</returns>
        public ClaimsIdentity GetSubjectIdentity() =>
            subject.Identity as ClaimsIdentity ?? subject.Identities.First();
    }

    /// <summary>
    /// The default <see cref="GetSubjectIdDelegate"/>: returns the value of the first non-empty claim whose type matches
    /// one of the supplied claim types, honoring the order of the supplied claim types (so an earlier claim type takes
    /// precedence over a later one).
    /// </summary>
    public static GetSubjectIdDelegate GetSubjectId { get; } =
        static (subject, subjectClaimTypes) =>
        {
            foreach (var claimType in subjectClaimTypes)
            {
                foreach (var claim in subject.Claims)
                {
                    if (claim.Type == claimType && !string.IsNullOrEmpty(claim.Value))
                    {
                        return claim.Value;
                    }
                }
            }

            return null;
        };
}
