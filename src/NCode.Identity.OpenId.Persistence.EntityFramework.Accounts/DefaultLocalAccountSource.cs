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
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Contexts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;

/// <summary>
/// Provides the generic Entity Framework reference implementation of <see cref="ILocalAccountSource"/> over
/// <see cref="ILocalAccountStore"/> and the pluggable <see cref="IPasswordHasher"/>. Registered as a singleton; it
/// creates a store manager per call via <see cref="IStoreManagerFactory"/>, so it holds no scoped state.
/// </summary>
internal sealed class DefaultLocalAccountSource(
    IStoreManagerFactory storeManagerFactory,
    IPasswordHasher passwordHasher
) : ILocalAccountSource
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPasswordHasher PasswordHasher { get; } = passwordHasher;

    /// <inheritdoc />
    public async ValueTask<LocalAccount?> ValidateCredentialsAsync(
        OpenIdContext openIdContext,
        string userName,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByUserNameOrDefaultAsync(userName, cancellationToken);
        if (account is null || !account.IsEnabled || string.IsNullOrEmpty(account.PasswordHash))
        {
            return null;
        }

        var result = PasswordHasher.VerifyHashedPassword(account.PasswordHash, password.Span);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Upgrade the stored hash to the current parameters without changing the credential.
            account.PasswordHash = PasswordHasher.HashPassword(password.Span);
            await store.UpdateAsync(account, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        return MapToLocalAccount(account);
    }

    /// <inheritdoc />
    public async ValueTask<LocalAccount?> FindBySubjectAsync(
        OpenIdContext openIdContext,
        string subject,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByIdOrDefaultAsync(subject, cancellationToken);
        return account is null ? null : MapToLocalAccount(account);
    }

    private static LocalAccount MapToLocalAccount(PersistedLocalAccount account) =>
        new()
        {
            Subject = account.LocalAccountId,
            IsEnabled = account.IsEnabled,
            SecurityStamp = account.SecurityStamp,
            Claims = account.Claims.Select(claim => new Claim(claim.Type, claim.Value)).ToList(),
        };
}
