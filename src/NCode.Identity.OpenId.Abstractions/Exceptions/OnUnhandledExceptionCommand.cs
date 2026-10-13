#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Environments;
using NCode.Mediator;
using NCode.Mediator.Middleware;

namespace NCode.Identity.OpenId.Exceptions;

/// <summary>
/// Represents a mediator command dispatched to handle an unexpected exception raised while processing an OpenID request.
/// If the <see cref="CommandExceptionHandlerState"/> indicates that the exception hasn't been handled,
/// then the OpenID middleware will perform its default exception handling logic by logging the exception as an error
/// and returning a standard OpenID error response.
/// </summary>
/// <param name="OpenIdEnvironment">The <see cref="OpenIdEnvironment"/> for the current request.</param>
/// <param name="HttpContext">The <see cref="HttpContext"/> for the current request.</param>
/// <param name="Mediator">The <see cref="IMediator"/> used to publish follow-up commands or notifications.</param>
/// <param name="ExceptionDispatchInfo">The captured <see cref="ExceptionDispatchInfo"/> for the exception being handled.</param>
/// <param name="ExceptionState">The <see cref="CommandExceptionHandlerState"/> a handler marks as handled once it has produced a response.</param>
[PublicAPI]
public readonly record struct OnUnhandledExceptionCommand(
    OpenIdEnvironment OpenIdEnvironment,
    HttpContext HttpContext,
    IMediator Mediator,
    ExceptionDispatchInfo ExceptionDispatchInfo,
    CommandExceptionHandlerState ExceptionState
) : ICommand;
