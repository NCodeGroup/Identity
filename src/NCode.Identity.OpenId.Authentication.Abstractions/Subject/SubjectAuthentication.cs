// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Contains the contextual information about the subject (the end-user) and how the subject was authenticated.
/// </summary>
/// <param name="AuthenticationScheme">The authentication scheme that authenticated the subject.</param>
/// <param name="AuthenticationProperties">The <see cref="AuthenticationProperties"/> from the authentication.</param>
/// <param name="Subject">The raw authenticated <see cref="ClaimsPrincipal"/> — the credential/identity-provider material
/// the authentication produced. Its claims are inputs; the authoritative subject identifier is <paramref name="PrincipalId"/>.</param>
/// <param name="PrincipalId">The stable, server-owned principal identifier resolved for the subject.
/// This single value — not the principal's raw <c>sub</c> claim — is what grants, consent, sessions, and revocation key
/// on, and what every token's <c>sub</c> is projected from, regardless of how the subject authenticated.</param>
// See ADR-1035.
[PublicAPI]
public readonly record struct SubjectAuthentication(
    string AuthenticationScheme,
    AuthenticationProperties AuthenticationProperties,
    ClaimsPrincipal Subject,
    string PrincipalId
)
{
    /// <summary>
    /// Gets the opaque <c>sid</c> of the end-user session this authentication belongs to, read from the subject's
    /// <see cref="JoseClaimNames.Payload.Sid"/> claim, or <c>null</c> when the subject was authenticated outside a
    /// server-side session.
    /// </summary>
    public string? SessionId => Subject.FindFirst(JoseClaimNames.Payload.Sid)?.Value;

    /// <summary>
    /// Creates a <see cref="SubjectAuthentication"/>, normalizing the <paramref name="subject"/>'s subject claims to
    /// <paramref name="principalId"/> so the <c>Subject.sub == PrincipalId</c> invariant holds by construction. This is
    /// the single sanctioned way every authentication entry point builds a ticket, so the raw credential's subject
    /// claim can never diverge from the resolved, server-owned principal id.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> whose tenant-aware
    /// <see cref="OpenIdContext.NormalizeSubjectId"/> seam rewrites the subject claims.</param>
    /// <param name="authenticationScheme">The authentication scheme that authenticated the subject.</param>
    /// <param name="authenticationProperties">The <see cref="AuthenticationProperties"/> from the authentication.</param>
    /// <param name="subject">The raw authenticated <see cref="ClaimsPrincipal"/> produced by the credential.</param>
    /// <param name="principalId">The stable, server-owned principal identifier resolved for the subject.</param>
    /// <returns>A <see cref="SubjectAuthentication"/> whose <see cref="Subject"/> carries <paramref name="principalId"/>
    /// as its subject claim.</returns>
    // See ADR-1035.
    public static SubjectAuthentication Create(
        OpenIdContext openIdContext,
        string authenticationScheme,
        AuthenticationProperties authenticationProperties,
        ClaimsPrincipal subject,
        string principalId
    )
    {
        var normalizedSubject = openIdContext.NormalizeSubjectId(subject, principalId);
        return new SubjectAuthentication(
            authenticationScheme,
            authenticationProperties,
            normalizedSubject,
            principalId
        );
    }
}
