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
[PublicAPI]
public readonly record struct SubjectAuthentication(
    string AuthenticationScheme,
    AuthenticationProperties AuthenticationProperties,
    ClaimsPrincipal Subject,
    string SubjectId
)
{
    /// <summary>
    /// Gets the opaque <c>sid</c> of the end-user session this authentication belongs to, read from the subject's
    /// <see cref="JoseClaimNames.Payload.Sid"/> claim, or <c>null</c> when the subject was authenticated outside a
    /// server-side session.
    /// </summary>
    public string? SessionId => Subject.FindFirst(JoseClaimNames.Payload.Sid)?.Value;
}
