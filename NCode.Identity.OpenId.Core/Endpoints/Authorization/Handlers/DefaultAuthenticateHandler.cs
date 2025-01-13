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

using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Extensions;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticateCommand"/> message.
/// </summary>
public class DefaultAuthenticateHandler(
    IOpenIdErrorFactory errorFactory,
    IAuthenticationSchemeProvider authenticationSchemeProvider
) : ICommandResponseHandler<AuthenticateCommand, AuthenticateSubjectResult>
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IAuthenticationSchemeProvider AuthenticationSchemeProvider { get; } = authenticationSchemeProvider;

    private bool DefaultSignInSchemeFetched { get; set; }
    private string? DefaultSignInSchemeName { get; set; }

    /// <inheritdoc />
    public async ValueTask<AuthenticateSubjectResult> HandleAsync(
        AuthenticateCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, _) = command;

        var httpContext = openIdContext.Http;

        var signInSchemeName = openIdClient.Settings.AuthorizationSignInScheme;
        if (string.IsNullOrEmpty(signInSchemeName))
        {
            if (!DefaultSignInSchemeFetched)
            {
                var signInScheme = await AuthenticationSchemeProvider.GetDefaultSignInSchemeAsync();
                DefaultSignInSchemeName = signInScheme?.Name;
                DefaultSignInSchemeFetched = true;
            }

            signInSchemeName = DefaultSignInSchemeName;
        }

        var baseResult = await httpContext.AuthenticateAsync(signInSchemeName);

        if (baseResult.None)
        {
            return default;
        }

        if (baseResult.Failure is not null)
        {
            var error = ErrorFactory
                .AccessDenied("Unable to authenticate the end-user.")
                .WithException(baseResult.Failure);
            return new AuthenticateSubjectResult(error);
        }

        Debug.Assert(baseResult.Succeeded);

        var baseTicket = baseResult.Ticket;
        var authenticationScheme = baseTicket.AuthenticationScheme;
        var authenticationProperties = baseTicket.Properties;
        var subject = baseTicket.Principal;

        // TODO
        var subjectId = subject.Claims.GetSubjectIdOrDefault() ??
                        throw new InvalidOperationException("The authenticated identity must contain a 'sub' claim.");

        var ticket = new SubjectAuthentication(
            authenticationScheme,
            authenticationProperties,
            subject,
            subjectId);

        return new AuthenticateSubjectResult(ticket);
    }
}
