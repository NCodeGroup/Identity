// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides shared helpers for emitting the client-facing <c>sub</c> claim through
/// <see cref="ISubjectIdentifierService"/>, so every token-issuance path emits the projected subject identifier the
/// same way and none can accidentally emit the raw stable principal identifier.
/// </summary>
internal static class SubjectIdentifierServiceExtensions
{
    /// <summary>
    /// Projects the authenticated subject's stable principal identifier to <paramref name="openIdClient"/>'s
    /// client-facing <c>sub</c> (public or pairwise) and adds it to <paramref name="targetClaims"/>.
    /// </summary>
    public static void AddProjectedSubjectClaim(
        this ISubjectIdentifierService subjectIdentifierService,
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        SubjectAuthentication subjectAuthentication,
        ICollection<Claim> targetClaims
    )
    {
        var subjectId = subjectIdentifierService.ProjectSubjectId(
            openIdContext,
            openIdClient,
            subjectAuthentication.PrincipalId
        );
        targetClaims.Add(new Claim(JoseClaimNames.Payload.Sub, subjectId));
    }
}
