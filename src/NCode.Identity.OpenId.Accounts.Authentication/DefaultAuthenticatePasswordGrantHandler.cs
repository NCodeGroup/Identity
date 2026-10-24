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

using System.Security.Claims;
using NCode.Buffers;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Provides the resource-owner password grant implementation of the
/// <see cref="ICommandResponseHandler{AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition}"/> seam, backed
/// by the configured <see cref="ILocalAccountStore"/>. This handler is registered only when the host opts into the
/// local-account authentication capability, so its mere presence is what enables the grant; the default
/// <c>NCode.Identity.OpenId.Authentication</c> handler reports the grant as unsupported when this capability is absent.
/// A store that rejects the credentials yields a failed (not unsupported) result, so the two cases stay distinct. A
/// password for an account that reached its limit of failed attempts is refused without being verified, as a wrong one
/// is.
/// </summary>
internal class DefaultAuthenticatePasswordGrantHandler(
    IStoreManagerFactory storeManagerFactory,
    IPrincipalResolver principalResolver,
    IAuditEventRecorder auditEventRecorder,
    IPasswordAttemptLimiter passwordAttemptLimiter
) : ICommandResponseHandler<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>
{
    internal const string InvalidCredentialsReason = "The provided credentials are invalid.";

    internal const string AttemptLimitReachedReason =
        "The account has reached its limit of failed password attempts.";

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;
    private IPasswordAttemptLimiter PasswordAttemptLimiter { get; } = passwordAttemptLimiter;

    internal virtual AuthenticateSubjectDisposition Failed() => new();

    internal virtual AuthenticateSubjectDisposition Authenticated(SubjectAuthentication ticket) =>
        new(ticket);

    /// <inheritdoc />
    public async ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticatePasswordGrantCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, _, tokenRequest, authenticationProperties) = command;

        var username = tokenRequest.Username;
        var password = tokenRequest.Password;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return Failed();
        }

        var limitedUserName = await ResolveLimitedUserNameAsync(username, cancellationToken);

        // A reached limit is answered as a wrong password, without verifying it. See ADR-0237.
        if (
            await PasswordAttemptLimiter.IsLimitReachedAsync(
                openIdContext,
                limitedUserName,
                cancellationToken
            )
        )
        {
            await AuditEventRecorder.RecordSubjectAuthenticationFailedAsync(
                openIdContext,
                AttemptLimitReachedReason,
                username,
                cancellationToken
            );
            return Failed();
        }

        var account = await VerifyCredentialsAsync(username, password, cancellationToken);
        if (account is null)
        {
            // A failure is recorded even when the client disconnects.
            await PasswordAttemptLimiter.RecordFailureAsync(
                openIdContext,
                limitedUserName,
                CancellationToken.None
            );
        }
        else
        {
            await PasswordAttemptLimiter.ResetAsync(
                openIdContext.Tenant.TenantId,
                limitedUserName,
                cancellationToken
            );
        }

        if (account is not { IsEnabled: true })
        {
            await AuditEventRecorder.RecordSubjectAuthenticationFailedAsync(
                openIdContext,
                InvalidCredentialsReason,
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
            account.LocalAccountId,
            cancellationToken
        );

        var ticket = SubjectAuthentication.Create(
            openIdContext,
            OpenIdConstants.GrantTypes.Password,
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

    // The limit counts the user name the store holds for the submitted one, so every spelling the store's database
    // matches (a trailing space under a padding collation, an accent under an accent-insensitive one) shares one
    // count; a name no account has is counted as submitted. See ADR-0237.
    private async ValueTask<string> ResolveLimitedUserNameAsync(
        string userName,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();
        var account = await store.GetByUserNameOrDefaultAsync(userName, cancellationToken);
        return account?.UserName ?? userName;
    }

    private async ValueTask<PersistedLocalAccount?> VerifyCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken
    )
    {
        // Encode the password into a pinned, zeroed-on-return buffer so the credential never lingers on the GC heap.
        var byteCount = SecureEncoding.UTF8.GetByteCount(password);
        using var owner = SecureMemoryPool<byte>.Shared.Rent(byteCount);
        var written = SecureEncoding.UTF8.GetBytes(password, owner.Memory.Span);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();
        var account = await store.VerifyCredentialAsync(
            userName,
            owner.Memory[..written],
            cancellationToken
        );

        // Persist a transparent rehash-on-verify, when one occurred, within this unit of work.
        if (account is not null)
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        return account;
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
