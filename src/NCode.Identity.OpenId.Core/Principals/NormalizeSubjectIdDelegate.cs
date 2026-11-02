// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.OpenId.Principals;

/// <summary>
/// Represents a delegate that rewrites the subject claims of a <see cref="ClaimsPrincipal"/> so the authoritative
/// subject identifier becomes <paramref name="subjectId"/>. This is the inverse of <see cref="GetSubjectIdDelegate"/>:
/// the returned principal must satisfy <c>GetSubjectId(result) == subjectId</c> for the same
/// <paramref name="subjectClaimTypes"/>.
/// </summary>
/// <param name="subject">The <see cref="ClaimsPrincipal"/> whose subject claims are rewritten.</param>
/// <param name="subjectId">The authoritative subject identifier to stamp onto the principal.</param>
/// <param name="subjectClaimTypes">The ordered claim types that identify the subject, in priority order.</param>
/// <returns>A <see cref="ClaimsPrincipal"/> whose subject identifier is <paramref name="subjectId"/>.</returns>
public delegate ClaimsPrincipal NormalizeSubjectIdDelegate(
    ClaimsPrincipal subject,
    string subjectId,
    IReadOnlyCollection<string> subjectClaimTypes
);
