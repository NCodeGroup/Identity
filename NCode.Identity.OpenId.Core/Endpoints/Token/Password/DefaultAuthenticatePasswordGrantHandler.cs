﻿#region Copyright Preamble

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

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Token.Password;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticatePasswordGrantCommand"/> message
/// that returns <see cref="SubjectAuthentication"/>.
/// </summary>
public class DefaultAuthenticatePasswordGrantHandler(
    ILogger<DefaultAuthenticatePasswordGrantHandler> logger
) : ICommandResponseHandler<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>
{
    private ILogger<DefaultAuthenticatePasswordGrantHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticatePasswordGrantCommand command,
        CancellationToken cancellationToken
    )
    {
        Logger.LogWarning(
            "The resource owner password credential grant type is not supported. " +
            "Please register an implementation of `ICommandResponseHandler<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>` " +
            "that can handle the resource owner password credential grant type."
        );

        var errorFactory = command.OpenIdContext.ErrorFactory;
        var error = errorFactory.UnsupportedGrantType("The resource owner password credential grant type is not supported.");

        return ValueTask.FromResult(new AuthenticateSubjectDisposition(error));
    }
}
