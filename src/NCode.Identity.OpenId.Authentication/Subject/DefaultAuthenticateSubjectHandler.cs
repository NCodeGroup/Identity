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
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Options;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticateSubjectCommand"/> message that
/// authenticates the subject using the ASP.NET Core authentication scheme configured by the host (the same mechanism
/// the management API relies on). Applications replace this handler (or configure the authentication scheme) to change
/// how the subject's credentials are validated.
/// </summary>
internal class DefaultAuthenticateSubjectHandler(
    IOptions<OpenIdOptions> optionsAccessor,
    IStoreManagerFactory storeManagerFactory,
    IPrincipalResolver principalResolver
) : ICommandResponseHandler<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>
{
    private OpenIdOptions Options { get; } = optionsAccessor.Value;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;

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

        // Resolve the authenticated subject to a stable, server-owned principal id, provisioning a federated principal
        // (and its connection identity) on first sight. This is the value carried into grants and emitted as the
        // `sub` claim, so authority and tokens reference the durable principal rather than the raw external subject
        // (ADR-0035). A subject with no resolvable upstream identity falls back to its raw subject id.
        var principalId = await ResolvePrincipalIdAsync(subject, subjectId, cancellationToken);

        var ticket = new SubjectAuthentication(
            authenticationScheme,
            authenticationProperties,
            subject,
            principalId
        );

        return Authenticated(ticket);
    }

    private async ValueTask<string> ResolvePrincipalIdAsync(
        ClaimsPrincipal subject,
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var principalId = await PrincipalResolver.ResolvePrincipalIdAsync(
            subject,
            storeManager,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);
        return string.IsNullOrEmpty(principalId) ? subjectId : principalId;
    }
}
