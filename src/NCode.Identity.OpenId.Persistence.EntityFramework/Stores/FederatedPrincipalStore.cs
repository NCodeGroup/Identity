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
/// Provides a default implementation of <see cref="IFederatedPrincipalStore"/> that uses Entity Framework Core for
/// persistence.
/// </summary>
internal class FederatedPrincipalStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStore<PersistedFederatedPrincipal, FederatedPrincipalEntity>, IFederatedPrincipalStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    protected override ValueTask<PersistedFederatedPrincipal> MapFromEntityAsync(
        FederatedPrincipalEntity entity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedFederatedPrincipal
            {
                PrincipalId = entity.PrincipalId,
                ConcurrencyToken = entity.ConcurrencyToken,
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<FederatedPrincipalEntity?> GetEntityOrDefaultAsync(
        Expression<Func<FederatedPrincipalEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext.FederatedPrincipals.FirstOrDefaultAsync(
                predicate,
                cancellationToken
            );
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<FederatedPrincipalEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext.FederatedPrincipals.AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        return await query.OrderBy(entity => entity.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(FederatedPrincipalEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedFederatedPrincipal principal,
        CancellationToken cancellationToken
    )
    {
        // The interceptor leaves a pre-seeded insert token intact (ADR-0012).
        principal.ConcurrencyToken = NextConcurrencyToken();

        var entity = new FederatedPrincipalEntity
        {
            Id = NextId(),
            PrincipalId = principal.PrincipalId,
            NormalizedPrincipalId = Normalize(principal.PrincipalId),
            ConcurrencyToken = principal.ConcurrencyToken,
        };

        await DbContext.FederatedPrincipals.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedFederatedPrincipal?> GetOrDefaultAsync(
        string principalId,
        CancellationToken cancellationToken
    )
    {
        var normalizedPrincipalId = Normalize(principalId);
        var entity = await GetEntityOrDefaultAsync(
            principal => principal.NormalizedPrincipalId == normalizedPrincipalId,
            cancellationToken
        );
        return entity is null ? null : await MapFromEntityAsync(entity, cancellationToken);
    }
}
