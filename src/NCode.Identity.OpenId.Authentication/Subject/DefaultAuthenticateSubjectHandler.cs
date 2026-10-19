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
/// Provides a default implementation of the handlers for the <see cref="AuthenticateSubjectCommand"/> and
/// <see cref="AuthenticateResourceRequestCommand"/> messages. An interactive endpoint authenticates the request's
/// session through the host's default authentication scheme (the session cookie). A protected resource such as
/// UserInfo authenticates only the access token the request presents, validated in-environment against the resolved
/// tenant's keys, issuer, and the required audience — the same in-environment validation the management API uses,
/// because the per-tenant signing keys and issuer are only known after the request environment resolves the tenant, so
/// a pre-routing authentication scheme cannot validate it. Applications replace these handlers to change how the
/// subject's credentials are validated.
/// </summary>
// See ADR-0044 and ADR-0155.
internal class DefaultAuthenticateSubjectHandler(
    IStoreManagerFactory storeManagerFactory,
    IPrincipalResolver principalResolver,
    IAuditEventRecorder auditEventRecorder
)
    : ICommandResponseHandler<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>,
        ICommandResponseHandler<AuthenticateResourceRequestCommand, AuthenticateSubjectDisposition>
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;

    private const string BearerPrefix = "Bearer ";
    private const string BearerScheme = "Bearer";
    private const string DPoPPrefix = "DPoP ";

    internal virtual AuthenticateSubjectDisposition Undefined() => new();

    internal virtual AuthenticateSubjectDisposition Failed(IOpenIdError error) => new(error);

    internal virtual AuthenticateSubjectDisposition Authenticated(SubjectAuthentication ticket) =>
        new(ticket);

    /// <inheritdoc />
    public async ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticateResourceRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, requiredAudiences) = command;

        // A protected resource is reached only with an access token (OpenID Connect Core 1.0 §5.3.1); an interactive
        // session never authenticates it, so a request without a token is undefined (ADR-0155).
        var accessToken = await ResolveBearerTokenAsync(openIdContext.Http);
        if (string.IsNullOrEmpty(accessToken))
        {
            return Undefined();
        }

        return await AuthenticateAccessTokenAsync(
            openIdContext,
            accessToken,
            requiredAudiences,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticateSubjectCommand command,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = command.OpenIdContext;
        var httpContext = openIdContext.Http;
        var errorFactory = openIdContext.ErrorFactory;

        // The request carries an interactive session, so use the host's default authenticate scheme (the session
        // cookie).
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
        IReadOnlyCollection<string> requiredAudiences,
        CancellationToken cancellationToken
    )
    {
        var disposition = await openIdContext.Mediator.SendAsync<
            AuthenticateAccessTokenCommand,
            AuthenticateAccessTokenDisposition
        >(
            new AuthenticateAccessTokenCommand(openIdContext, accessToken, requiredAudiences),
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

    private static async ValueTask<string?> ResolveBearerTokenAsync(HttpContext httpContext)
    {
        // RFC 6750 §2.1: the 'Authorization' header is the preferred (and here, primary) transport.
        if (TryGetAuthorizationHeaderToken(httpContext, out var headerToken))
        {
            return headerToken;
        }

        // RFC 6750 §2.2: a client MAY send the access token as a form-encoded body parameter. Honor it only for a
        // non-GET request whose body is exactly 'application/x-www-form-urlencoded' (the spec's single-part body
        // requirement, which excludes multipart). The URI-query form (§2.3) is intentionally unsupported because
        // RFC 9700 deprecates passing access tokens in the query string.
        var request = httpContext.Request;
        if (
            !HttpMethods.IsGet(request.Method)
            && request.HasFormContentType
            && request.ContentType is { } contentType
            && contentType.StartsWith(
                "application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            var form = await request.ReadFormAsync();
            var bodyToken = form[OpenIdConstants.Parameters.AccessToken].ToString();
            if (!string.IsNullOrEmpty(bodyToken))
            {
                return bodyToken;
            }
        }

        return null;
    }

    private static bool TryGetAuthorizationHeaderToken(HttpContext httpContext, out string token)
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
