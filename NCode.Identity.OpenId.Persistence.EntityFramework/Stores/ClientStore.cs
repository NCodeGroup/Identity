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
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IClientStore"/> that uses Entity Framework Core for persistence.
/// </summary>
[PublicAPI]
public class ClientStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext dbContext
) : BaseStore<PersistedClient, ClientEntity>, IClientStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = dbContext;

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedClient persistedClient,
        CancellationToken cancellationToken)
    {
        var tenant = await GetTenantAsync(persistedClient, cancellationToken);

        var clientId = NextId(persistedClient.Id);
        var urls = new List<ClientUrlEntity>(persistedClient.RedirectUrls.Count);
        var secrets = new List<ClientSecretEntity>(persistedClient.Secrets.Count);

        var clientEntity = new ClientEntity
        {
            Id = clientId,
            TenantId = tenant.Id,
            ClientId = persistedClient.ClientId,
            NormalizedClientId = Normalize(persistedClient.ClientId),
            ConcurrencyToken = NextConcurrencyToken(),
            IsDisabled = persistedClient.IsDisabled,
            Settings = persistedClient.Settings,
            Tenant = tenant,
            Urls = urls,
            Secrets = secrets
        };

        foreach (var url in persistedClient.RedirectUrls)
        {
            var clientUrlEntity = new ClientUrlEntity
            {
                Id = NextId(),
                TenantId = tenant.Id,
                ClientId = clientId,
                UrlType = UrlTypes.RedirectUrl,
                UrlValue = url,
                Tenant = tenant,
                Client = clientEntity
            };

            urls.Add(clientUrlEntity);

            await DbContext.ClientUrls.AddAsync(clientUrlEntity, cancellationToken);
        }

        foreach (var persistedSecret in persistedClient.Secrets)
        {
            var secretEntity = MapNew(tenant, persistedSecret);

            await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

            var clientSecretEntity = new ClientSecretEntity
            {
                Id = NextId(),

                Tenant = tenant,
                TenantId = tenant.Id,

                Client = clientEntity,
                ClientId = clientEntity.Id,

                Secret = secretEntity,
                SecretId = secretEntity.Id,
            };

            secrets.Add(clientSecretEntity);

            await DbContext.ClientSecrets.AddAsync(clientSecretEntity, cancellationToken);
        }

        await DbContext.Clients.AddAsync(clientEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(
        PersistedClient persistedClient,
        CancellationToken cancellationToken)
    {
        var clientEntity = await GetEntityByIdAsync(persistedClient.Id, cancellationToken);

        clientEntity.ConcurrencyToken = NextConcurrencyToken();
        clientEntity.IsDisabled = persistedClient.IsDisabled;
        clientEntity.Settings = persistedClient.Settings;
    }

    /// <inheritdoc />
    public override async ValueTask RemoveByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var client = await TryGetEntityAsync(client => client.Id == id, cancellationToken);
        if (client is null)
            return;

        var tenantId = client.TenantId;

        DbContext.ClientUrls.RemoveRange(
            client.Urls.Where(url =>
                url.TenantId == tenantId &&
                url.ClientId == id));

        var clientSecrets = DbContext.ClientSecrets.Where(clientSecret =>
            clientSecret.TenantId == tenantId &&
            clientSecret.ClientId == id);

        DbContext.Secrets.RemoveRange(
            clientSecrets.Select(clientSecret => clientSecret.Secret));

        DbContext.ClientSecrets.RemoveRange(
            clientSecrets);

        DbContext.Clients.Remove(client);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedClient?> TryGetByClientIdAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = Normalize(tenantId);
        var normalizedClientId = Normalize(clientId);

        return await TryGetAsync(
            client =>
                client.Tenant.NormalizedTenantId == normalizedTenantId &&
                client.NormalizedClientId == normalizedClientId,
            cancellationToken);
    }

    //

    /// <inheritdoc />
    protected override ValueTask<PersistedClient> MapAsync(
        ClientEntity client,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new PersistedClient
        {
            Id = client.Id,
            TenantId = client.Tenant.TenantId,
            ClientId = client.ClientId,
            ConcurrencyToken = client.ConcurrencyToken,
            IsDisabled = client.IsDisabled,
            Settings = client.Settings,
            Secrets = MapExisting(client.Secrets),
            RedirectUrls = client.Urls
                .Where(url => url.UrlType == UrlTypes.RedirectUrl)
                .Select(url => url.UrlValue)
                .ToList(),
            // TODO: RequireRequestObject
        });
    }

    /// <inheritdoc />
    protected override async ValueTask<ClientEntity?> TryGetEntityAsync(
        Expression<Func<ClientEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        return await DbContext.Clients
            .Include(client => client.Tenant)
            .Include(client => client.Urls)
            .Include(client => client.Secrets)
            .ThenInclude(clientSecret => clientSecret.Secret)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }
}
