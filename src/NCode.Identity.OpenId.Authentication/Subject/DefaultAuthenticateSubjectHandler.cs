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
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticateSubjectCommand"/> message. When the
/// request presents a bearer access token (the UserInfo path) it validates that self-issued token against the resolved
/// tenant's keys, issuer, and the OpenID identity audience — the same in-environment validation the management API uses,
/// because the per-tenant signing keys and issuer are only known after the request environment resolves the
/// tenant, so a pre-routing authentication scheme cannot validate it. When no bearer token is present the request
/// carries an interactive session, so it falls back to the host's default authentication scheme (the session cookie).
/// Applications replace this handler to change how the subject's credentials are validated.
/// </summary>
// See ADR-0044.
internal class DefaultAuthenticateSubjectHandler(
    IStoreManagerFactory storeManagerFactory,
    IPrincipalResolver principalResolver,
    IAuditEventRecorder auditEventRecorder
) : ICommandResponseHandler<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;

    private const string BearerPrefix = "Bearer ";
    private const string BearerScheme = "Bearer";
    private const string DPoPPrefix = "DPoP ";

    // A UserInfo access token binds the OpenID identity resource server, which owns the openid/profile/email scopes
    // (ADR-0027); require that audience so a token minted for a different resource server is rejected.
    private static readonly string[] UserInfoAudiences =
    [
        OpenIdConstants.SystemResourceServerIdentifiers.OpenId,
    ];

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

        // A presented bearer access token (the UserInfo path) is validated in-environment against the resolved tenant,
        // not via a pre-routing authentication scheme the host cannot tenant-scope (ADR-0044).
        if (TryGetBearerToken(httpContext, out var accessToken))
        {
            return await AuthenticateAccessTokenAsync(
                openIdContext,
                accessToken,
                cancellationToken
            );
        }

        // No bearer token: the request carries an interactive session, so use the host's default authenticate scheme
        // (the session cookie).
        var baseResult = await httpContext.AuthenticateAsync();

        if (baseResult.None)
        {
            return Undefined();
        }

        if (baseResult.Failure is not null)
        {
            await AuditEventRecorder.RecordSubjectAuthenticationFailedAsync(
                openIdContext,
                "Failed to authenticate the end-user.",
                attemptedSubjectId: null,
                cancellationToken
            );
            return Failed(
                errorFactory
                    .AccessDenied("Failed to authenticate the end-user.")
                    .WithException(baseResult.Failure)
            );
        }

        Debug.Assert(baseResult.Succeeded);

        var baseTicket = baseResult.Ticket;
        return await CompleteAsync(
            openIdContext,
            baseTicket.Principal,
            baseTicket.AuthenticationScheme,
            baseTicket.Properties,
            cancellationToken
        );
    }

    private async ValueTask<AuthenticateSubjectDisposition> AuthenticateAccessTokenAsync(
        OpenIdContext openIdContext,
        string accessToken,
        CancellationToken cancellationToken
    )
    {
        var disposition = await openIdContext.Mediator.SendAsync<
            AuthenticateAccessTokenCommand,
            AuthenticateAccessTokenDisposition
        >(
            new AuthenticateAccessTokenCommand(openIdContext, accessToken, UserInfoAudiences),
            cancellationToken
        );

        if (disposition.HasError)
        {
            await AuditEventRecorder.RecordSubjectAuthenticationFailedAsync(
                openIdContext,
                "Failed to authenticate the end-user.",
                attemptedSubjectId: null,
                cancellationToken
            );
            return Failed(disposition.Error);
        }

        if (!disposition.IsAuthenticated)
        {
            return Undefined();
        }

        return await CompleteAsync(
            openIdContext,
            disposition.Principal,
            BearerScheme,
            new AuthenticationProperties(),
            cancellationToken
        );
    }

    private async ValueTask<AuthenticateSubjectDisposition> CompleteAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal subject,
        string authenticationScheme,
        AuthenticationProperties authenticationProperties,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdContext.ErrorFactory;

        var subjectId = openIdContext.GetSubjectId(subject);
        if (string.IsNullOrEmpty(subjectId))
        {
            await AuditEventRecorder.RecordSubjectAuthenticationFailedAsync(
                openIdContext,
                "Unable to determine the end-user's subject id.",
                attemptedSubjectId: null,
                cancellationToken
            );
            return Failed(
                errorFactory.AccessDenied("Unable to determine the end-user's subject id.")
            );
        }

        // Resolve the authenticated subject to a stable, server-owned principal id, provisioning a federated principal
        // (and its connection identity) on first sight. This is the value carried into grants and emitted as the
        // `sub` claim, so authority and tokens reference the durable principal rather than the raw external subject
        // (ADR-0035). A subject with no resolvable upstream identity falls back to its raw subject id.
        var principalId = await ResolvePrincipalIdAsync(
            openIdContext,
            subject,
            subjectId,
            cancellationToken
        );

        var ticket = SubjectAuthentication.Create(
            openIdContext,
            authenticationScheme,
            authenticationProperties,
            subject,
            principalId
        );

        await AuditEventRecorder.RecordSubjectAuthenticatedAsync(
            openIdContext,
            principalId,
            cancellationToken
        );

        return Authenticated(ticket);
    }

    private static bool TryGetBearerToken(HttpContext httpContext, out string token)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();

        // Accept both the Bearer scheme and the DPoP scheme (RFC 9449 §7.1); the DPoP proof itself is validated when
        // the sender-constrained token's cnf claim is checked.
        foreach (var prefix in (ReadOnlySpan<string>)[BearerPrefix, DPoPPrefix])
        {
            if (authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                token = authorization[prefix.Length..].Trim();
                return token.Length > 0;
            }
        }

        token = string.Empty;
        return false;
    }

    private async ValueTask<string> ResolvePrincipalIdAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal subject,
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var principalId = await PrincipalResolver.ResolvePrincipalIdAsync(
            openIdContext,
            subject,
            storeManager,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);
        return string.IsNullOrEmpty(principalId) ? subjectId : principalId;
    }
}
