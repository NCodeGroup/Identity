﻿#region Copyright Preamble

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

using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Mediator;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Commands;

/// <summary>
/// Defines an <see cref="ICommand{TResponse}"/> contract that accepts <see cref="AuthorizationRequestContext"/> and
/// <see cref="AuthenticationTicket"/> as input arguments and expects <see cref="IAuthorizationTicket"/> as a response.
/// </summary>
/// <param name="AuthorizationRequestContext">The <see cref="AuthorizationRequestContext"/> input argument for the command contract.</param>
/// <param name="AuthenticationTicket">The <see cref="AuthenticationTicket"/> input argument for the command contract.</param>
public record struct CreateAuthorizationTicketCommand(
    AuthorizationRequestContext AuthorizationRequestContext,
    AuthenticationTicket AuthenticationTicket
) : ICommand<IAuthorizationTicket>;