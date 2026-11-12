// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.OpenId.Principals;

/// <summary>
/// Provides the default subject delegates (<see cref="GetSubjectIdentityDelegate"/>, <see cref="GetSubjectIdDelegate"/>,
/// and <see cref="NormalizeSubjectIdDelegate"/>) that <see cref="OpenIdOptions"/> starts with.
/// </summary>
internal static class DefaultSubjectDelegates
{
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

    /// <summary>
    /// The default <see cref="NormalizeSubjectIdDelegate"/>: strips every subject-type claim from the principal and
    /// stamps a single claim of the primary subject claim type (the first of the supplied claim types) holding
    /// <c>subjectId</c> onto the primary identity, preserving its authentication type and name/role claim types and all
    /// of its other claims. The result round-trips through <see cref="GetSubjectId"/> to <c>subjectId</c>, which is the
    /// <c>Subject.sub == PrincipalId</c> invariant every ticket-construction site relies on.
    /// </summary>
    public static NormalizeSubjectIdDelegate NormalizeSubjectId { get; } =
        static (subject, subjectId, subjectClaimTypes) =>
        {
            var primaryClaimType = subjectClaimTypes.First();
            var primaryIdentity = subject.GetSubjectIdentity();

            var identities = subject.Identities.Select(identity =>
            {
                var claims = identity.Claims.Where(claim =>
                    !subjectClaimTypes.Contains(claim.Type)
                );

                if (ReferenceEquals(identity, primaryIdentity))
                {
                    claims = claims.Append(new Claim(primaryClaimType, subjectId));
                }

                return new ClaimsIdentity(
                    claims,
                    identity.AuthenticationType,
                    identity.NameClaimType,
                    identity.RoleClaimType
                );
            });

            return new ClaimsPrincipal(identities);
        };

    extension(ClaimsPrincipal subject)
    {
        /// <summary>
        /// Returns the default implementation of <see cref="GetSubjectIdentityDelegate"/> that extracts the primary <see cref="ClaimsIdentity"/> from a <see cref="ClaimsPrincipal"/>.
        /// </summary>
        /// <returns>The <see cref="ClaimsIdentity"/> from the <see cref="ClaimsPrincipal"/>.</returns>
        public ClaimsIdentity GetSubjectIdentity() =>
            subject.Identity as ClaimsIdentity ?? subject.Identities.First();
    }
}
