#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.Jose;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Contains the contextual information about the subject (the end-user) and how the subject was authenticated.
/// </summary>
/// <param name="AuthenticationScheme">The authentication scheme that authenticated the subject.</param>
/// <param name="AuthenticationProperties">The <see cref="AuthenticationProperties"/> from the authentication.</param>
/// <param name="Subject">The raw authenticated <see cref="ClaimsPrincipal"/> — the credential/identity-provider material
/// the authentication produced. Its claims are inputs; the authoritative subject identifier is <paramref name="PrincipalId"/>.</param>
/// <param name="PrincipalId">The stable, server-owned principal identifier resolved for the subject
/// (<see href="https://github.com/NCodeGroup/Identity/blob/main/docs/adr/0035-federated-principals-and-identity-resolution.md">ADR-0035</see>).
/// This single value — not the principal's raw <c>sub</c> claim — is what grants, consent, sessions, and revocation key
/// on, and what every token's <c>sub</c> is projected from, regardless of how the subject authenticated.</param>
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
}
