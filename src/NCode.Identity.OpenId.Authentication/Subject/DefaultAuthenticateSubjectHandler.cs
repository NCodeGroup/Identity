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

using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Options;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticateSubjectCommand"/> message that
/// authenticates the subject using the ASP.NET Core authentication scheme configured by the host (the same mechanism
/// the management API relies on). Applications replace this handler (or configure the authentication scheme) to change
/// how the subject's credentials are validated.
/// </summary>
internal class DefaultAuthenticateSubjectHandler(IOptions<OpenIdOptions> optionsAccessor)
    : ICommandResponseHandler<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>
{
    private OpenIdOptions Options { get; } = optionsAccessor.Value;

    internal virtual AuthenticateSubjectDisposition Undefined() => new();

    internal virtual AuthenticateSubjectDisposition Failed(IOpenIdError error) => new(error);

    internal virtual AuthenticateSubjectDisposition Authenticated(SubjectAuthentication ticket) =>
        new(ticket);

    /// <inheritdoc />
    public async ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticateSubjectCommand command,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = command.OpenIdContext;
        var httpContext = openIdContext.Http;
        var errorFactory = openIdContext.ErrorFactory;

        // Uses the host's default authenticate scheme (for example, a JWT bearer scheme), matching the management API.
        var baseResult = await httpContext.AuthenticateAsync();

        if (baseResult.None)
        {
            return Undefined();
        }

        if (baseResult.Failure is not null)
        {
            return Failed(
                errorFactory
                    .AccessDenied("Failed to authenticate the end-user.")
                    .WithException(baseResult.Failure)
            );
        }

        Debug.Assert(baseResult.Succeeded);

        var baseTicket = baseResult.Ticket;
        var authenticationScheme = baseTicket.AuthenticationScheme;
        var authenticationProperties = baseTicket.Properties;
        var subject = baseTicket.Principal;

        var subjectId = Options.GetSubjectId(subject);
        if (string.IsNullOrEmpty(subjectId))
        {
            return Failed(
                errorFactory.AccessDenied("Unable to determine the end-user's subject id.")
            );
        }

        var ticket = new SubjectAuthentication(
            authenticationScheme,
            authenticationProperties,
            subject,
            subjectId
        );

        return Authenticated(ticket);
    }
}
