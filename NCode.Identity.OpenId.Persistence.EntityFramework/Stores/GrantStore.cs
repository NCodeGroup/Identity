#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

internal class GrantStore(
    IIdGenerator<long> idGenerator,
    OpenIdDbContext dbContext
) : BaseStore, IGrantStore
{
    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = dbContext;

    /// <inheritdoc />
    public async ValueTask AddAsync(PersistedGrant persistedGrant, CancellationToken cancellationToken)
    {
        var tenant = await GetTenantAsync(persistedGrant, cancellationToken);

        var normalizedClientId = Normalize(persistedGrant.ClientId);

        var client = await DbContext.Clients.FirstOrDefaultAsync(
            client =>
                client.TenantId == tenant.Id &&
                client.NormalizedClientId == normalizedClientId,
            cancellationToken);

        if (persistedGrant.ClientId is not null && client is null)
            throw new InvalidOperationException($"Client '{persistedGrant.ClientId}' not found.");

        var grantEntity = new GrantEntity
        {
            Id = NextId(persistedGrant.Id),
            TenantId = tenant.Id,
            GrantType = persistedGrant.GrantType,
            HashedKey = persistedGrant.HashedKey,
            ConcurrencyToken = NextConcurrencyToken(),
            ClientId = client?.Id,
            SubjectId = persistedGrant.SubjectId,
            NormalizedSubjectId = Normalize(persistedGrant.SubjectId),
            CreatedWhen = persistedGrant.CreatedWhen.ToUniversalTime(),
            ExpiresWhen = persistedGrant.ExpiresWhen.ToUniversalTime(),
            ConsumedWhen = persistedGrant.ConsumedWhen?.ToUniversalTime(),
            Payload = persistedGrant.Payload,
            Tenant = tenant,
            Client = client
        };

        await DbContext.Grants.AddAsync(grantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RemoveByIdAsync(long id, CancellationToken cancellationToken)
    {
        var grant = await TryGetEntityAsync(grant => grant.Id == id, cancellationToken);
        if (grant is null)
            return;

        DbContext.Grants.Remove(grant);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant?> TryGetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await TryGetAsync(grant => grant.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant?> TryGetAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = Normalize(tenantId);

        return await TryGetAsync(
            grant =>
                grant.Tenant.NormalizedTenantId == normalizedTenantId &&
                grant.GrantType == grantType &&
                grant.HashedKey == hashedKey,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask SetConsumedOnceAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = Normalize(tenantId);

        var grant = await TryGetEntityAsync(
            grant =>
                grant.Tenant.NormalizedTenantId == normalizedTenantId &&
                grant.GrantType == grantType &&
                grant.HashedKey == hashedKey,
            cancellationToken);

        if (grant is null)
            return;

        if (grant.ConsumedWhen is not null)
            return;

        grant.ConcurrencyToken = NextConcurrencyToken();
        grant.ConsumedWhen = consumedWhen.ToUniversalTime();
    }

    //

    private async ValueTask<PersistedGrant?> TryGetAsync(
        Expression<Func<GrantEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var client = await TryGetEntityAsync(predicate, cancellationToken);
        return client is null ? null : Map(client);
    }

    private async ValueTask<GrantEntity?> TryGetEntityAsync(
        Expression<Func<GrantEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        return await DbContext.Grants
            .Include(grant => grant.Tenant)
            .Include(grant => grant.Client)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    private static PersistedGrant Map(GrantEntity grant) => new()
    {
        Id = grant.Id,
        TenantId = grant.Tenant.TenantId,
        GrantType = grant.GrantType,
        HashedKey = grant.HashedKey,
        ClientId = grant.Client?.ClientId,
        SubjectId = grant.SubjectId,
        CreatedWhen = grant.CreatedWhen,
        ExpiresWhen = grant.ExpiresWhen,
        ConsumedWhen = grant.ConsumedWhen,
        Payload = grant.Payload
    };
}
