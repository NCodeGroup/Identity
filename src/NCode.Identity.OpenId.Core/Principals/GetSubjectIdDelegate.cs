// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.OpenId.Principals;

/// <summary>
/// Represents a delegate that extracts the subject id from a <see cref="ClaimsPrincipal"/> using an ordered list of
/// candidate claim types.
/// </summary>
/// <param name="subject">The <see cref="ClaimsPrincipal"/> to search for the subject id.</param>
/// <param name="subjectClaimTypes">The ordered claim types to try, in priority order.</param>
/// <returns>The subject id if found; otherwise <c>null</c>.</returns>
public delegate string? GetSubjectIdDelegate(
    ClaimsPrincipal subject,
    IReadOnlyCollection<string> subjectClaimTypes
);
