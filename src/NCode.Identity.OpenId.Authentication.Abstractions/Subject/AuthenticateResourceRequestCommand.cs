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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Represents a mediator command that authenticates the subject (the end-user) of a request to one of the server's own
/// protected resources, such as <c>UserInfo</c>, from the access token the request presents (<c>RFC 6750</c> §2, or a
/// DPoP-bound token per <c>RFC 9449</c> §7.1), returning an <see cref="AuthenticateSubjectDisposition"/>. Only an access
/// token authenticates such a request — never an interactive session — so the subject carries the token's claims,
/// including the <c>client_id</c> of the client it was issued to.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="RequiredAudiences">The audiences the access token must target; the token is rejected unless it targets
/// at least one of them.</param>
// See ADR-0155.
[PublicAPI]
public readonly record struct AuthenticateResourceRequestCommand(
    OpenIdContext OpenIdContext,
    IReadOnlyCollection<string> RequiredAudiences
) : ICommand<AuthenticateSubjectDisposition>;
