// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
