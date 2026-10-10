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
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;

/// <summary>
/// Provides the generic Entity Framework reference implementation of <see cref="ILocalAccountProvisioner"/>: hashes the
/// credential with the pluggable <see cref="IPasswordHasher"/>, generates the opaque account id and security stamp, and
/// persists the account together with its one-to-one self-issued federated identity and owning principal (ADR-0057).
/// Each operation stages its changes into the caller's unit of work and does not save, so the caller controls the
/// transaction boundary. Registered as a singleton; it holds no scoped state.
/// </summary>
internal sealed class DefaultLocalAccountProvisioner(
    IPasswordHasher passwordHasher,
    ICryptoService cryptoService
) : ILocalAccountProvisioner
{
    private IPasswordHasher PasswordHasher { get; } = passwordHasher;
    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public async ValueTask<string> CreateAsync(
        OpenIdContext openIdContext,
        IStoreManager storeManager,
        LocalAccountCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        var tenantId = openIdContext.Tenant.TenantId;
        var localAccountId = CryptoService.GenerateResourceId();
        var principalId = CryptoService.GenerateResourceId();

        // A local account is a self-issued connection of a human actor: the account, its owning principal, and the
        // one-to-one self-issued federated identity are created together so ownership and principal-level metadata have
        // a stable target from the moment the account exists (ADR-0057). The principal is added first so the identity
        // store resolves it from the unit of work's tracked (unsaved) entities.
        var principalStore = storeManager.GetStore<IFederatedPrincipalStore>();
        await principalStore.AddAsync(
            new PersistedFederatedPrincipal
            {
                TenantId = tenantId,
                PrincipalId = principalId,
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );

        var identityStore = storeManager.GetStore<IFederatedIdentityStore>();
        await identityStore.AddAsync(
            new PersistedFederatedIdentity
            {
                TenantId = tenantId,
                FederatedIdentityId = CryptoService.GenerateResourceId(),
                PrincipalId = principalId,
                Issuer = AccountConstants.SelfIssuer,
                Subject = localAccountId,
                JoinKey = null,
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );

        var accountStore = storeManager.GetStore<ILocalAccountStore>();
        await accountStore.AddAsync(
            new PersistedLocalAccount
            {
                TenantId = tenantId,
                LocalAccountId = localAccountId,
                UserName = request.UserName,
                Email = request.Email,
                EmailVerified = request.EmailVerified,
                PasswordHash = PasswordHasher.HashPassword(request.Password.Span),
                SecurityStamp = CryptoService.GenerateResourceId(),
                IsEnabled = request.IsEnabled,
                Claims = [],
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );

        return localAccountId;
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(
        OpenIdContext openIdContext,
        IStoreManager storeManager,
        LocalAccountUpdateRequest request,
        CancellationToken cancellationToken
    )
    {
        var account = await GetAccountAsync(
            storeManager,
            request.LocalAccountId,
            cancellationToken
        );

        account.UserName = request.UserName;
        account.Email = request.Email;
        account.EmailVerified = request.EmailVerified;

        await storeManager.GetStore<ILocalAccountStore>().UpdateAsync(account, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask SetEnabledAsync(
        OpenIdContext openIdContext,
        IStoreManager storeManager,
        string localAccountId,
        bool isEnabled,
        CancellationToken cancellationToken
    )
    {
        var account = await GetAccountAsync(storeManager, localAccountId, cancellationToken);

        account.IsEnabled = isEnabled;

        await storeManager.GetStore<ILocalAccountStore>().UpdateAsync(account, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask ResetPasswordAsync(
        OpenIdContext openIdContext,
        IStoreManager storeManager,
        LocalAccountPasswordResetRequest request,
        CancellationToken cancellationToken
    )
    {
        var account = await GetAccountAsync(
            storeManager,
            request.LocalAccountId,
            cancellationToken
        );

        account.PasswordHash = PasswordHasher.HashPassword(request.Password.Span);
        // Rotating the security stamp invalidates outstanding sessions and tokens issued before the reset.
        account.SecurityStamp = CryptoService.GenerateResourceId();

        await storeManager.GetStore<ILocalAccountStore>().UpdateAsync(account, cancellationToken);
    }

    private static async ValueTask<PersistedLocalAccount> GetAccountAsync(
        IStoreManager storeManager,
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        return account
            ?? throw new InvalidOperationException(
                $"A local account with '{localAccountId}' was not found."
            );
    }
}
