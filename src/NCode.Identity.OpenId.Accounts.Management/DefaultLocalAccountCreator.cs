// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Management;

/// <summary>
/// Provides the default implementation of <see cref="ILocalAccountCreator"/>.
/// </summary>
internal sealed class DefaultLocalAccountCreator(
    ILocalAccountValidator localAccountValidator,
    ICryptoService cryptoService,
    LocalAccountPasswords localAccountPasswords
) : ILocalAccountCreator
{
    private ILocalAccountValidator LocalAccountValidator { get; } = localAccountValidator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private LocalAccountPasswords LocalAccountPasswords { get; } = localAccountPasswords;

    /// <inheritdoc />
    public async ValueTask<LocalAccountCreation> StageAsync(
        IStoreManager storeManager,
        string tenantId,
        CreateLocalAccountRequest request,
        CancellationToken cancellationToken
    )
    {
        var error = await LocalAccountValidator.ValidateCreateAsync(
            request.UserName,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return new LocalAccountCreation { Error = error };
        }

        var localAccountId = CryptoService.GenerateResourceId();
        var principalId = CryptoService.GenerateResourceId();

        var account = new PersistedLocalAccount
        {
            TenantId = tenantId,
            LocalAccountId = localAccountId,
            UserName = request.UserName,
            Email = request.Email,
            EmailVerified = request.EmailVerified,
            IsEnabled = request.IsEnabled,
            Claims =
                request
                    .Claims?.Select(claim => new PersistedLocalAccountClaim
                    {
                        Type = claim.Type,
                        Value = claim.Value,
                    })
                    .ToList()
                ?? [],
            ConcurrencyToken = string.Empty,
        };

        var passwordError = await LocalAccountPasswords.ValidateAsync(
            storeManager,
            tenantId,
            account,
            request.Password,
            cancellationToken
        );
        if (passwordError is not null)
        {
            return new LocalAccountCreation { Error = passwordError };
        }

        // Eagerly create the owning principal and the account's self-issued connection so the account resolves to a
        // principal uniformly with any federated identity (ADR-1051/ADR-1057).
        var principal = new PersistedFederatedPrincipal
        {
            TenantId = tenantId,
            PrincipalId = principalId,
            ConcurrencyToken = string.Empty,
        };

        // Set-when-present: a missing bag leaves the principal's default empty object (ADR-1055).
        if (request.ProfileMetadata is { } profileMetadata)
        {
            principal.ProfileMetadata = profileMetadata;
        }

        if (request.SystemMetadata is { } systemMetadata)
        {
            principal.SystemMetadata = systemMetadata;
        }

        await storeManager
            .GetStore<IFederatedPrincipalStore>()
            .AddAsync(principal, cancellationToken);

        var identity = new PersistedFederatedIdentity
        {
            TenantId = tenantId,
            FederatedIdentityId = CryptoService.GenerateResourceId(),
            PrincipalId = principalId,
            Issuer = AccountConstants.SelfIssuer,
            Subject = localAccountId,
            JoinKey = null,
            ConcurrencyToken = string.Empty,
        };
        await storeManager
            .GetStore<IFederatedIdentityStore>()
            .AddAsync(identity, cancellationToken);

        await LocalAccountPasswords.WithPasswordBytesAsync(
            request.Password,
            (bytes, ct) => storeManager.GetStore<ILocalAccountStore>().AddAsync(account, bytes, ct),
            cancellationToken
        );

        return new LocalAccountCreation { PrincipalId = principalId, Account = account };
    }
}
