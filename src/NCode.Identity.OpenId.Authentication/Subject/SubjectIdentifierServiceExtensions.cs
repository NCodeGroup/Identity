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
