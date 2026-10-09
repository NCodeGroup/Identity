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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticatePasswordGrantCommand"/> message that
/// returns <see cref="SubjectAuthentication"/>. The resource-owner password grant is supported only when the host has
/// registered an <see cref="ILocalAccountSource"/>; otherwise the grant is reported as unsupported. A registered source
/// that rejects the credentials yields a failed (not unsupported) result, so the two cases stay distinct.
/// </summary>
internal class DefaultAuthenticatePasswordGrantHandler(
    ILogger<DefaultAuthenticatePasswordGrantHandler> logger,
    IStoreManagerFactory storeManagerFactory,
    IPrincipalResolver principalResolver,
    IAuditEventRecorder auditEventRecorder,
    ILocalAccountSource? localAccountSource = null
) : ICommandResponseHandler<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>
{
    private ILogger<DefaultAuthenticatePasswordGrantHandler> Logger { get; } = logger;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;
    private ILocalAccountSource? LocalAccountSource { get; } = localAccountSource;

    internal virtual AuthenticateSubjectDisposition NotSupported(IOpenIdError error) => new(error);

    internal virtual AuthenticateSubjectDisposition Failed() => new();

    internal virtual AuthenticateSubjectDisposition Authenticated(SubjectAuthentication ticket) =>
        new(ticket);

    /// <inheritdoc />
    public async ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticatePasswordGrantCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, _, tokenRequest) = command;
        var errorFactory = openIdContext.ErrorFactory;

        // No source registered: the server does not offer the grant (distinct from a credential failure below).
        if (LocalAccountSource is not { } localAccountSource)
        {
            Logger.PasswordGrantNotSupported();
            return NotSupported(
                errorFactory.UnsupportedGrantType(
                    "The resource owner password credential grant type is not supported."
                )
            );
        }

        var username = tokenRequest.Username;
        var password = tokenRequest.Password;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return Failed();
        }

        var account = await ValidateCredentialsAsync(
            openIdContext,
            localAccountSource,
            username,
            password,
            cancellationToken
        );
        if (account is null)
        {
            await AuditEventRecorder.RecordSubjectAuthenticationFailedAsync(
                openIdContext,
                "The provided credentials are invalid.",
                username,
                cancellationToken
            );
            return Failed();
        }

        var subject = await openIdContext.Mediator.SendAsync<
            CreatePasswordGrantSubjectCommand,
            ClaimsPrincipal
        >(new CreatePasswordGrantSubjectCommand(openIdContext, account), cancellationToken);

        var principalId = await ResolvePrincipalIdAsync(
            openIdContext,
            subject,
            account.Subject,
            cancellationToken
        );

        var ticket = new SubjectAuthentication(
            OpenIdConstants.GrantTypes.Password,
            new AuthenticationProperties(), // TODO: shouldn't this be provided earlier in this flow/command/handler?
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

    private static async ValueTask<LocalAccount?> ValidateCredentialsAsync(
        OpenIdContext openIdContext,
        ILocalAccountSource localAccountSource,
        string userName,
        string password,
        CancellationToken cancellationToken
    )
    {
        // Encode the password into a pinned, zeroed-on-return buffer so the credential never lingers on the GC heap.
        var byteCount = SecureEncoding.UTF8.GetByteCount(password);
        using var owner = SecureMemoryPool<byte>.Shared.Rent(byteCount);
        var written = SecureEncoding.UTF8.GetBytes(password, owner.Memory.Span);

        return await localAccountSource.ValidateCredentialsAsync(
            openIdContext,
            userName,
            owner.Memory[..written],
            cancellationToken
        );
    }

    private async ValueTask<string> ResolvePrincipalIdAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal subject,
        string fallbackSubjectId,
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
        return string.IsNullOrEmpty(principalId) ? fallbackSubjectId : principalId;
    }
}
