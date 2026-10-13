#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;

/// <summary>
/// Represents a mediator command that creates the <see cref="IAuthorizationTicket"/> for a successfully authorized
/// request — the artifact (such as the authorization code, and any front-channel tokens) returned to the client in the
/// authorization response.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the authorization request.</param>
/// <param name="AuthorizationRequest">The authorized <see cref="IAuthorizationRequest"/> the ticket is created for.</param>
/// <param name="SubjectAuthentication">The <see cref="SubjectAuthentication"/> identifying the authenticated end-user and their session.</param>
[PublicAPI]
public readonly record struct CreateAuthorizationTicketCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IAuthorizationRequest AuthorizationRequest,
    SubjectAuthentication SubjectAuthentication
) : ICommand<IAuthorizationTicket>;
