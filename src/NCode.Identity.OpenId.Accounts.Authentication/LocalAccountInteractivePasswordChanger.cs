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
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Provides the local-account implementation of the <see cref="IInteractivePasswordChanger"/> seam, backed by the
/// configured <see cref="ILocalAccountStore"/>. It resolves the signed-in end user's single enabled local account,
/// verifies the current password through the same credential path the interactive sign-in uses, and replaces the
/// password in one unit of work. It judges the new password with the password policy before it verifies the current
/// one, and verifies the current password under the limit on failed attempts. An end user without exactly one enabled
/// local account has no password to change here.
/// </summary>
// See ADR-0230.
internal sealed class LocalAccountInteractivePasswordChanger(
    IStoreManagerFactory storeManagerFactory,
    IPasswordPolicy passwordPolicy,
    IPasswordAttemptLimiter passwordAttemptLimiter
) : IInteractivePasswordChanger
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPasswordPolicy PasswordPolicy { get; } = passwordPolicy;
    private IPasswordAttemptLimiter PasswordAttemptLimiter { get; } = passwordAttemptLimiter;

    /// <inheritdoc />
    public async ValueTask<InteractivePasswordChangeResult> ChangePasswordAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken
    )
    {
        var principalId = openIdContext.GetSubjectId(principal);
        if (string.IsNullOrEmpty(principalId))
        {
            return InteractivePasswordChangeResult.NotSupported;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var identities = await storeManager
            .GetStore<IFederatedIdentityStore>()
            .GetByPrincipalAsync(principalId, cancellationToken);

        // A principal that links several local accounts has no single password to change.
        var selfIssuedIdentities = identities
            .Where(identity =>
                string.Equals(
                    identity.Issuer,
                    AccountConstants.SelfIssuer,
                    StringComparison.Ordinal
                )
            )
            .ToList();
        if (selfIssuedIdentities.Count != 1)
        {
            return InteractivePasswordChangeResult.NotSupported;
        }

        var accountStore = storeManager.GetStore<ILocalAccountStore>();
        var account = await accountStore.GetByIdOrDefaultAsync(
            selfIssuedIdentities[0].Subject,
            cancellationToken
        );
        if (account is not { IsEnabled: true })
        {
            return InteractivePasswordChangeResult.NotSupported;
        }

        // The new password is judged before the current one is verified, so a password the policy refuses costs no
        // verification. See ADR-0236.
        var newPasswordMemory = newPassword.AsMemory();
        var policyContext = new PasswordPolicyContext
        {
            Settings = openIdContext.Tenant.SettingsProvider.Collection,
            UserName = account.UserName,
            Email = account.Email,
        };
        var policyResult = await PasswordPolicy.ValidateAsync(
            policyContext,
            newPasswordMemory,
            cancellationToken
        );
        if (policyResult.IsRejected)
        {
            return InteractivePasswordChangeResult.Refused(policyResult.Error);
        }

        // A reached limit is answered as a wrong password, without verifying it. See ADR-0237.
        if (
            await PasswordAttemptLimiter.IsLimitReachedAsync(
                openIdContext,
                account.UserName,
                cancellationToken
            )
        )
        {
            return InteractivePasswordChangeResult.CurrentPasswordInvalid;
        }

        // Encode each password into its own pinned, zeroed-on-return buffer so the credentials never linger on the GC
        // heap.
        var currentByteCount = SecureEncoding.UTF8.GetByteCount(currentPassword);
        using var currentOwner = SecureMemoryPool<byte>.Shared.Rent(currentByteCount);
        var currentWritten = SecureEncoding.UTF8.GetBytes(
            currentPassword,
            currentOwner.Memory.Span
        );

        var verified = await accountStore.VerifyCredentialAsync(
            account.UserName,
            currentOwner.Memory[..currentWritten],
            cancellationToken
        );
        if (
            verified is null
            || !string.Equals(
                verified.LocalAccountId,
                account.LocalAccountId,
                StringComparison.Ordinal
            )
        )
        {
            // A failure is recorded even when the client disconnects.
            await PasswordAttemptLimiter.RecordFailureAsync(
                openIdContext,
                account.UserName,
                CancellationToken.None
            );
            return InteractivePasswordChangeResult.CurrentPasswordInvalid;
        }

        await PasswordAttemptLimiter.ResetAsync(
            openIdContext.Tenant.TenantId,
            account.UserName,
            cancellationToken
        );

        var newByteCount = SecureEncoding.UTF8.GetByteCount(newPassword);
        using var newOwner = SecureMemoryPool<byte>.Shared.Rent(newByteCount);
        var newWritten = SecureEncoding.UTF8.GetBytes(newPassword, newOwner.Memory.Span);

        // The verification's transparent rehash, when one occurred, commits with the new password.
        await accountStore.SetPasswordAsync(
            account.LocalAccountId,
            newOwner.Memory[..newWritten],
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        return InteractivePasswordChangeResult.Changed;
    }
}
