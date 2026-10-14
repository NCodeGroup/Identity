#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
