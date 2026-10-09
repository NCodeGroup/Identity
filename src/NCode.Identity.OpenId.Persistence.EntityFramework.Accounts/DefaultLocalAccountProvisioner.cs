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

using NCode.Identity.Logic;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Contexts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;

/// <summary>
/// Provides the generic Entity Framework reference implementation of <see cref="ILocalAccountProvisioner"/>: hashes the
/// credential with the pluggable <see cref="IPasswordHasher"/>, generates the opaque account id and security stamp, and
/// persists the account through <see cref="ILocalAccountStore"/>. Registered as a singleton; it creates a store manager
/// per call via <see cref="IStoreManagerFactory"/>, so it holds no scoped state.
/// </summary>
internal sealed class DefaultLocalAccountProvisioner(
    IStoreManagerFactory storeManagerFactory,
    IPasswordHasher passwordHasher,
    ICryptoService cryptoService
) : ILocalAccountProvisioner
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPasswordHasher PasswordHasher { get; } = passwordHasher;
    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public async ValueTask<string> CreateAsync(
        OpenIdContext openIdContext,
        LocalAccountCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        var localAccountId = CryptoService.GenerateResourceId();

        var account = new PersistedLocalAccount
        {
            TenantId = openIdContext.Tenant.TenantId,
            LocalAccountId = localAccountId,
            UserName = request.UserName,
            Email = request.Email,
            EmailVerified = request.EmailVerified,
            PasswordHash = PasswordHasher.HashPassword(request.Password.Span),
            SecurityStamp = CryptoService.GenerateResourceId(),
            IsEnabled = request.IsEnabled,
            ProfileMetadata = request.ProfileMetadata,
            SystemMetadata = request.SystemMetadata,
            Claims = [],
            ConcurrencyToken = string.Empty,
        };

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();
        await store.AddAsync(account, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return localAccountId;
    }
}
