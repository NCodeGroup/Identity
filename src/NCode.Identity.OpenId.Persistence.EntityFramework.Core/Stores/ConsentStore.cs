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
using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IConsentStore"/> that uses Entity Framework Core for persistence.
/// Consent records are upserted by their natural (tenant, client, subject) key.
/// </summary>
internal class ConsentStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : CoreBaseStore<PersistedConsent, ConsentEntity>, IConsentStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    protected override ValueTask<PersistedConsent> MapFromEntityAsync(
        ConsentEntity entity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedConsent
            {
                TenantId = entity.Tenant.TenantId,
                ClientId = entity.Client.ClientId,
                SubjectId = entity.SubjectId,
                GrantedScopes = DeserializeScopes(entity.GrantedScopesJson),
                CreatedWhen = entity.CreatedWhen,
                UpdatedWhen = entity.UpdatedWhen,
                ConcurrencyToken = entity.ConcurrencyToken,
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<ConsentEntity?> GetEntityOrDefaultAsync(
        Expression<Func<ConsentEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext
                .Consents.Include(entity => entity.Tenant)
                .Include(entity => entity.Client)
                .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<ConsentEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext
            .Consents.Include(entity => entity.Tenant)
            .Include(entity => entity.Client)
            .AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        return await query.OrderBy(entity => entity.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(ConsentEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask<PersistedConsent?> GetOrDefaultAsync(
        string clientId,
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        var normalizedClientId = Normalize(clientId);
        var normalizedSubjectId = Normalize(subjectId);
        var entity = await GetEntityOrDefaultAsync(
            consent =>
                consent.Client.NormalizedClientId == normalizedClientId
                && consent.NormalizedSubjectId == normalizedSubjectId,
            cancellationToken
        );
        return entity is null ? null : await MapFromEntityAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedConsent>> GetBySubjectAsync(
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        var normalizedSubjectId = Normalize(subjectId);
        var entities = await DbContext
            .Consents.Include(entity => entity.Tenant)
            .Include(entity => entity.Client)
            .Where(entity => entity.NormalizedSubjectId == normalizedSubjectId)
            .ToListAsync(cancellationToken);

        var result = new List<PersistedConsent>(entities.Count);
        foreach (var entity in entities)
        {
            result.Add(await MapFromEntityAsync(entity, cancellationToken));
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(PersistedConsent consent, CancellationToken cancellationToken)
    {
        var normalizedClientId = Normalize(consent.ClientId);
        var normalizedSubjectId = Normalize(consent.SubjectId);
        var scopesJson = SerializeScopes(consent.GrantedScopes);

        var existing = await GetEntityOrDefaultAsync(
            entity =>
                entity.Client.NormalizedClientId == normalizedClientId
                && entity.NormalizedSubjectId == normalizedSubjectId,
            cancellationToken
        );

        if (existing is not null)
        {
            // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-0012).
            existing.GrantedScopesJson = scopesJson;
            existing.UpdatedWhen = consent.UpdatedWhen.ToUniversalTime();
            DbContext.Consents.Update(existing);
            return;
        }

        var tenantEntity = await GetTenantEntityAsync(consent.TenantId, cancellationToken);

        var clientEntity =
            await DbContext.Clients.FirstOrDefaultAsync(
                entity =>
                    entity.TenantId == tenantEntity.Id
                    && entity.NormalizedClientId == normalizedClientId,
                cancellationToken
            ) ?? throw new InvalidOperationException($"Client '{consent.ClientId}' not found.");

        var entity = new ConsentEntity
        {
            Id = NextId(),
            TenantId = tenantEntity.Id,
            NormalizedTenantId = tenantEntity.NormalizedTenantId,
            ClientId = clientEntity.Id,
            SubjectId = consent.SubjectId,
            NormalizedSubjectId = normalizedSubjectId,
            GrantedScopesJson = scopesJson,
            CreatedWhen = consent.CreatedWhen.ToUniversalTime(),
            UpdatedWhen = consent.UpdatedWhen.ToUniversalTime(),
            ConcurrencyToken = NextConcurrencyToken(),
            Tenant = tenantEntity,
            Client = clientEntity,
        };

        await DbContext.Consents.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(
        string clientId,
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        var normalizedClientId = Normalize(clientId);
        var normalizedSubjectId = Normalize(subjectId);
        var entity = await GetEntityOrDefaultAsync(
            consent =>
                consent.Client.NormalizedClientId == normalizedClientId
                && consent.NormalizedSubjectId == normalizedSubjectId,
            cancellationToken
        );
        if (entity is null)
        {
            return;
        }

        DbContext.Consents.Remove(entity);
    }

    private static string SerializeScopes(IReadOnlyCollection<string> scopes) =>
        JsonSerializer.Serialize(scopes);

    private static IReadOnlyCollection<string> DeserializeScopes(string json) =>
        string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<IReadOnlyCollection<string>>(json) ?? [];
}
