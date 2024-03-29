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
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Mediator;

namespace NIdentity.OpenId.Endpoints.Authorization.Commands;

public class ValidateUserIsActiveResult
{
    /// <summary>
    /// Gets a <see cref="bool"/> value indicating whether the user is active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Sets the user as inactive.
    /// </summary>
    public void SetInactive() => IsActive = false;
}

public record struct ValidateUserIsActiveCommand(
    AuthorizationRequestContext Context,
    AuthenticationTicket Ticket,
    ValidateUserIsActiveResult Result
) : ICommand;
