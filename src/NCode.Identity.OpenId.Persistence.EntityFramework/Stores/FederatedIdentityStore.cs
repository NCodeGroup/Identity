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
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IFederatedIdentityStore"/> that uses Entity Framework Core for
/// persistence.
/// </summary>
internal class FederatedIdentityStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStore<PersistedFederatedIdentity, FederatedIdentityEntity>, IFederatedIdentityStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    protected override ValueTask<PersistedFederatedIdentity> MapFromEntityAsync(
        FederatedIdentityEntity entity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedFederatedIdentity
            {
                FederatedIdentityId = entity.FederatedIdentityId,
                PrincipalId = entity.FederatedPrincipal.PrincipalId,
                Issuer = entity.Issuer,
                Subject = entity.Subject,
                JoinKey = entity.JoinKey,
                ConcurrencyToken = entity.ConcurrencyToken,
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<FederatedIdentityEntity?> GetEntityOrDefaultAsync(
        Expression<Func<FederatedIdentityEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext
                .FederatedIdentities.Include(entity => entity.FederatedPrincipal)
                .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<FederatedIdentityEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext
            .FederatedIdentities.Include(entity => entity.FederatedPrincipal)
            .AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        return await query.OrderBy(entity => entity.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(FederatedIdentityEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedFederatedIdentity identity,
        CancellationToken cancellationToken
    )
    {
        var normalizedPrincipalId = Normalize(identity.PrincipalId);
        var principalEntity =
            GetLocalPrincipalOrDefault(normalizedPrincipalId)
            ?? await DbContext.FederatedPrincipals.SingleOrDefaultAsync(
                principal => principal.NormalizedPrincipalId == normalizedPrincipalId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"A federated principal with '{identity.PrincipalId}' was not found."
            );

        // The interceptor leaves a pre-seeded insert token intact (ADR-0012).
        identity.ConcurrencyToken = NextConcurrencyToken();

        var entity = new FederatedIdentityEntity
        {
            Id = NextId(),
            FederatedIdentityId = identity.FederatedIdentityId,
            NormalizedFederatedIdentityId = Normalize(identity.FederatedIdentityId),
            FederatedPrincipalId = principalEntity.Id,
            Issuer = identity.Issuer,
            NormalizedIssuer = Normalize(identity.Issuer),
            Subject = identity.Subject,
            NormalizedSubject = Normalize(identity.Subject),
            JoinKey = identity.JoinKey,
            NormalizedJoinKey = Normalize(identity.JoinKey),
            ConcurrencyToken = identity.ConcurrencyToken,
            FederatedPrincipal = principalEntity,
        };

        await DbContext.FederatedIdentities.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedFederatedIdentity?> GetOrDefaultAsync(
        string federatedIdentityId,
        CancellationToken cancellationToken
    )
    {
        var normalizedFederatedIdentityId = Normalize(federatedIdentityId);
        var entity = await GetEntityOrDefaultAsync(
            identity => identity.NormalizedFederatedIdentityId == normalizedFederatedIdentityId,
            cancellationToken
        );
        return entity is null ? null : await MapFromEntityAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedFederatedIdentity?> GetByIssuerSubjectAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken
    )
    {
        var normalizedIssuer = Normalize(issuer);
        var normalizedSubject = Normalize(subject);
        var entity = await GetEntityOrDefaultAsync(
            identity =>
                identity.NormalizedIssuer == normalizedIssuer
                && identity.NormalizedSubject == normalizedSubject,
            cancellationToken
        );
        return entity is null ? null : await MapFromEntityAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedFederatedIdentity>> GetByPrincipalAsync(
        string principalId,
        CancellationToken cancellationToken
    )
    {
        var normalizedPrincipalId = Normalize(principalId);
        var entities = await DbContext
            .FederatedIdentities.Include(entity => entity.FederatedPrincipal)
            .Where(entity =>
                entity.FederatedPrincipal.NormalizedPrincipalId == normalizedPrincipalId
            )
            .ToListAsync(cancellationToken);

        var result = new List<PersistedFederatedIdentity>(entities.Count);
        foreach (var entity in entities)
        {
            result.Add(await MapFromEntityAsync(entity, cancellationToken));
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedFederatedIdentity>> GetByJoinKeyAsync(
        string joinKey,
        CancellationToken cancellationToken
    )
    {
        var normalizedJoinKey = Normalize(joinKey);
        var entities = await DbContext
            .FederatedIdentities.Include(entity => entity.FederatedPrincipal)
            .Where(entity => entity.NormalizedJoinKey == normalizedJoinKey)
            .ToListAsync(cancellationToken);

        var result = new List<PersistedFederatedIdentity>(entities.Count);
        foreach (var entity in entities)
        {
            result.Add(await MapFromEntityAsync(entity, cancellationToken));
        }

        return result;
    }

    private FederatedPrincipalEntity? GetLocalPrincipalOrDefault(string? normalizedPrincipalId) =>
        DbContext
            .Set<FederatedPrincipalEntity>()
            .Local.FirstOrDefault(principal =>
                principal.NormalizedPrincipalId == normalizedPrincipalId
            );
}
