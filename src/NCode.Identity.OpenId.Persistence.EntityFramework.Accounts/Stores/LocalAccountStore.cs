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

using System.Linq.Expressions;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Entities;
using NCode.Identity.OpenId.Persistence.EntityFramework.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores;

/// <summary>
/// Provides the generic Entity Framework Core implementation of <see cref="ILocalAccountStore"/> over the shared
/// <see cref="OpenIdDbContext"/>, reusing the framework store base for id/concurrency generation and normalization.
/// Credential handling is owned here: it hashes and verifies passwords through the pluggable <see cref="IPasswordHasher"/>
/// and keeps the hash a store-internal detail that never leaves on the <see cref="PersistedLocalAccount"/> contract.
/// </summary>
internal sealed class LocalAccountStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    IPasswordHasher passwordHasher,
    OpenIdDbContext openIdDbContext
) : BaseStore<PersistedLocalAccount, LocalAccountEntity>, ILocalAccountStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    private IPasswordHasher PasswordHasher { get; } = passwordHasher;

    private DbSet<LocalAccountEntity> Accounts => DbContext.Set<LocalAccountEntity>();

    /// <inheritdoc />
    protected override ValueTask<PersistedLocalAccount> MapFromEntityAsync(
        LocalAccountEntity entity,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(MapFromEntity(entity));

    /// <inheritdoc />
    protected override async ValueTask<LocalAccountEntity?> GetEntityOrDefaultAsync(
        Expression<Func<LocalAccountEntity, bool>> predicate,
        CancellationToken cancellationToken
    ) =>
        await Accounts
            .Include(entity => entity.Claims)
            .FirstOrDefaultAsync(predicate, cancellationToken);

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<LocalAccountEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = Accounts.Include(entity => entity.Claims).AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        return await query.OrderBy(entity => entity.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(LocalAccountEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedLocalAccount account,
        ReadOnlyMemory<byte>? password,
        CancellationToken cancellationToken
    )
    {
        account.ConcurrencyToken = NextConcurrencyToken();

        var entity = new LocalAccountEntity
        {
            Id = NextId(),
            TenantId = account.TenantId,
            NormalizedTenantId = Normalize(account.TenantId),
            LocalAccountId = account.LocalAccountId,
            NormalizedLocalAccountId = Normalize(account.LocalAccountId),
            UserName = account.UserName,
            NormalizedUserName = Normalize(account.UserName),
            Email = account.Email,
            NormalizedEmail = Normalize(account.Email),
            EmailVerified = account.EmailVerified,
            PasswordHash = password is { } value ? PasswordHasher.HashPassword(value.Span) : null,
            SecurityStamp = NextSecurityStamp(),
            IsEnabled = account.IsEnabled,
            ConcurrencyToken = account.ConcurrencyToken,
        };

        foreach (var claim in account.Claims)
        {
            entity.Claims.Add(
                new LocalAccountClaimEntity
                {
                    Id = NextId(),
                    NormalizedTenantId = entity.NormalizedTenantId,
                    Type = claim.Type,
                    Value = claim.Value,
                }
            );
        }

        await Accounts.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(
        PersistedLocalAccount account,
        CancellationToken cancellationToken
    )
    {
        var entity = await GetEntityByIdAsync(account.LocalAccountId, cancellationToken);

        entity.UserName = account.UserName;
        entity.NormalizedUserName = Normalize(account.UserName);
        entity.Email = account.Email;
        entity.NormalizedEmail = Normalize(account.Email);
        entity.EmailVerified = account.EmailVerified;
        entity.IsEnabled = account.IsEnabled;
    }

    /// <inheritdoc />
    public async ValueTask SetPasswordAsync(
        string localAccountId,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    )
    {
        var entity = await GetEntityByIdAsync(localAccountId, cancellationToken);

        entity.PasswordHash = PasswordHasher.HashPassword(password.Span);
        // Rotating the security stamp invalidates outstanding sessions and tokens issued before the reset.
        entity.SecurityStamp = NextSecurityStamp();
    }

    /// <inheritdoc />
    public async ValueTask<PersistedLocalAccount?> VerifyCredentialAsync(
        string userName,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    )
    {
        var normalizedUserName = Normalize(userName);
        var entity = await GetEntityOrDefaultAsync(
            candidate => candidate.NormalizedUserName == normalizedUserName,
            cancellationToken
        );
        if (entity is null || !entity.IsEnabled || string.IsNullOrEmpty(entity.PasswordHash))
        {
            return null;
        }

        var result = PasswordHasher.VerifyHashedPassword(entity.PasswordHash, password.Span);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Transparently upgrade the stored hash to the current parameters without changing the credential.
            entity.PasswordHash = PasswordHasher.HashPassword(password.Span);
        }

        return MapFromEntity(entity);
    }

    /// <inheritdoc />
    public ValueTask<PersistedLocalAccount?> GetByIdOrDefaultAsync(
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        var normalizedLocalAccountId = Normalize(localAccountId);
        return GetOrDefaultAsync(
            entity => entity.NormalizedLocalAccountId == normalizedLocalAccountId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<PersistedLocalAccount?> GetByUserNameOrDefaultAsync(
        string userName,
        CancellationToken cancellationToken
    )
    {
        var normalizedUserName = Normalize(userName);
        return await GetOrDefaultAsync(
            entity => entity.NormalizedUserName == normalizedUserName,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string localAccountId, CancellationToken cancellationToken)
    {
        var entity = await GetEntityByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        DbContext.Set<LocalAccountClaimEntity>().RemoveRange(entity.Claims);
        Accounts.Remove(entity);
    }

    /// <inheritdoc />
    public async ValueTask ReplaceClaimsAsync(
        string localAccountId,
        IReadOnlyList<PersistedLocalAccountClaim> claims,
        CancellationToken cancellationToken
    )
    {
        var entity = await GetEntityByIdAsync(localAccountId, cancellationToken);

        DbContext.Set<LocalAccountClaimEntity>().RemoveRange(entity.Claims);
        entity.Claims.Clear();

        foreach (var claim in claims)
        {
            entity.Claims.Add(
                new LocalAccountClaimEntity
                {
                    Id = NextId(),
                    NormalizedTenantId = entity.NormalizedTenantId,
                    Type = claim.Type,
                    Value = claim.Value,
                }
            );
        }

        // Touch the account row so its concurrency token bumps (the claims collection change alone would not mark it).
        entity.ConcurrencyToken = NextConcurrencyToken();
    }

    private static string NextSecurityStamp() => Guid.NewGuid().ToString("N");

    private ValueTask<LocalAccountEntity?> GetEntityByIdOrDefaultAsync(
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        var normalizedLocalAccountId = Normalize(localAccountId);
        return GetEntityOrDefaultAsync(
            entity => entity.NormalizedLocalAccountId == normalizedLocalAccountId,
            cancellationToken
        );
    }

    private async ValueTask<LocalAccountEntity> GetEntityByIdAsync(
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        var entity = await GetEntityByIdOrDefaultAsync(localAccountId, cancellationToken);
        return entity
            ?? throw new InvalidOperationException(
                $"A local account with '{localAccountId}' was not found."
            );
    }

    private static PersistedLocalAccount MapFromEntity(LocalAccountEntity entity) =>
        new()
        {
            TenantId = entity.TenantId,
            LocalAccountId = entity.LocalAccountId,
            UserName = entity.UserName,
            Email = entity.Email,
            EmailVerified = entity.EmailVerified,
            SecurityStamp = entity.SecurityStamp,
            IsEnabled = entity.IsEnabled,
            Claims = entity
                .Claims.Select(claim => new PersistedLocalAccountClaim
                {
                    Type = claim.Type,
                    Value = claim.Value,
                })
                .ToList(),
            ConcurrencyToken = entity.ConcurrencyToken,
        };
}
