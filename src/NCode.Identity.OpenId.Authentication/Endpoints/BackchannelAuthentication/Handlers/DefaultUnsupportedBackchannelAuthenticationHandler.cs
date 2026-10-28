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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;

/// <summary>
/// Provides the default (capability-absent) implementation of the
/// <see cref="ICommandResponseHandler{AuthenticateBackchannelRequestCommand, AuthenticateSubjectDisposition}"/> seam
/// that reports the CIBA login hint as unresolvable. A deployment registers a login-hint resolution capability that
/// replaces this handler; without it a backchannel authentication request is rejected with <c>unknown_user_id</c>.
/// </summary>
internal class DefaultUnsupportedBackchannelAuthenticationHandler
    : ICommandResponseHandler<AuthenticateBackchannelRequestCommand, AuthenticateSubjectDisposition>
{
    /// <inheritdoc />
    public ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticateBackchannelRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = command.OpenIdContext.ErrorFactory;

        var error = errorFactory
            .Create(OpenIdProtocol.ErrorCodes.UnknownUserId)
            .WithDescription("The supplied login hint could not be resolved to a known user.")
            .WithStatusCode(StatusCodes.Status400BadRequest);

        return ValueTask.FromResult(new AuthenticateSubjectDisposition(error));
    }
}
