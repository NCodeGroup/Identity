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
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticateCommand"/> message.
/// </summary>
internal class DefaultAuthenticateHandler(ILogger<DefaultAuthenticateHandler> logger)
    : ICommandResponseHandler<AuthenticateCommand, AuthenticateSubjectDisposition>
{
    private ILogger<DefaultAuthenticateHandler> Logger { get; } = logger;

    internal virtual AuthenticateSubjectDisposition Undefined() => new();

    internal virtual AuthenticateSubjectDisposition Failed(IOpenIdError error) => new(error);

    internal virtual AuthenticateSubjectDisposition Authenticated(SubjectAuthentication ticket) =>
        new(ticket);

    /// <inheritdoc />
    public async ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticateCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, _) = command;

        var httpContext = openIdContext.Http;
        var errorFactory = openIdContext.ErrorFactory;

        // the default is "Identity.Application" which is compatible with Microsoft.AspNetCore.Identity
        // this scheme returns the local application user identity but still allows SSO logins from external identity providers
        // do not use the "Identity.External" scheme as it is only for external identity providers
        var authenticateSchemeName = openIdClient.Settings.GetValue(
            OpenIdSettingKeys.AuthorizationAuthenticateScheme
        );
        if (authenticateSchemeName == string.Empty)
        {
            authenticateSchemeName = null;
        }

        var baseResult = await httpContext.AuthenticateAsync(authenticateSchemeName);

        if (baseResult.None)
        {
            return Undefined();
        }

        // A present-but-invalid session cookie (undecryptable after a data-protection key change, expired, or tampered)
        // is NOT an access-denied error: the end-user simply has no valid session. Treat it as unauthenticated so the
        // authorize flow prompts for interactive login (or returns login_required under prompt=none); a successful login
        // re-issues a fresh cookie, so the stale cookie self-heals instead of dead-ending the user with access_denied.
        // The failure is logged (not swallowed) so a systemic authentication fault stays diagnosable.
        if (baseResult.Failure is not null)
        {
            Logger.EndUserSessionAuthenticationFailed(baseResult.Failure);
            return Undefined();
        }

        Debug.Assert(baseResult.Succeeded);

        var baseTicket = baseResult.Ticket;
        var authenticationScheme = baseTicket.AuthenticationScheme;
        var authenticationProperties = baseTicket.Properties;
        var subject = baseTicket.Principal;

        // The stable public subject is carried through the grant unchanged; the per-client pairwise projection (for a
        // client whose subject_type is pairwise) is applied where the `sub` is emitted toward the client, so grants,
        // consent, sessions, and revocation keep referencing the stable principal (ISubjectIdentifierService).
        var subjectId = openIdContext.GetSubjectId(subject);
        if (string.IsNullOrEmpty(subjectId))
        {
            return Failed(
                errorFactory.AccessDenied("Unable to determine the the end-user's subject id.")
            );
        }

        var ticket = SubjectAuthentication.Create(
            openIdContext,
            authenticationScheme,
            authenticationProperties,
            subject,
            subjectId
        );

        return Authenticated(ticket);
    }
}
