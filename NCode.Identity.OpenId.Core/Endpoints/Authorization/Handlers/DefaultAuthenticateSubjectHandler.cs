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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticateSubjectCommand"/> message.
/// </summary>
[PublicAPI]
public class DefaultAuthenticateSubjectHandler(
    IOptions<OpenIdOptions> optionsAccessor,
    IOpenIdErrorFactory errorFactory,
    IAuthenticationSchemeProvider authenticationSchemeProvider
) : ICommandResponseHandler<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>
{
    private OpenIdOptions Options { get; } = optionsAccessor.Value;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IAuthenticationSchemeProvider AuthenticationSchemeProvider { get; } = authenticationSchemeProvider;

    private bool DefaultSignInSchemeFetched { get; set; }
    private string? DefaultSignInSchemeName { get; set; }

    internal virtual AuthenticateSubjectDisposition Failed(IOpenIdError error) => new(error);
    internal virtual AuthenticateSubjectDisposition Authenticated(SubjectAuthentication ticket) => new(ticket);

    /// <inheritdoc />
    public async ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticateSubjectCommand command,
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
            return Failed(ErrorFactory.AccessDenied("Failed to authenticate the end-user.").WithException(baseResult.Failure));
        }

        Debug.Assert(baseResult.Succeeded);

        var baseTicket = baseResult.Ticket;
        var authenticationScheme = baseTicket.AuthenticationScheme;
        var authenticationProperties = baseTicket.Properties;
        var subject = baseTicket.Principal;

        var subjectId = Options.GetSubjectId(subject);
        if (string.IsNullOrEmpty(subjectId))
        {
            return Failed(ErrorFactory.AccessDenied("Unable to determine the the end-user's subject id."));
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
