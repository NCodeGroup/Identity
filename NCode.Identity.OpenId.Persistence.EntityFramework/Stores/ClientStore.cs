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
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IClientStore"/> that uses Entity Framework Core for persistence.
/// </summary>
[PublicAPI]
public class ClientStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStoreWithEntityId<PersistedClient, ClientEntity>, IClientStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    public override bool IsRemoveSupported => true;

    /// <inheritdoc />
    protected override ValueTask<PersistedClient> MapAsync(
        ClientEntity client,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(new PersistedClient
        {
            Id = client.Id,
            TenantId = client.Tenant.TenantId,
            ClientId = client.ClientId,
            ConcurrencyToken = client.ConcurrencyToken,
            IsDisabled = client.IsDisabled,
            SettingsState = ConcurrentStateFactory.Create(client.SettingsJson, client.SettingsConcurrencyToken),
            SecretsState = ConcurrentStateFactory.Create(MapExisting(client.Secrets), client.SecretsConcurrencyToken),
            RedirectUrls = client.Urls
                .Where(url => url.UrlType == UrlTypes.RedirectUrl)
                .Select(url => url.UrlValue)
                .ToList(),
        });
    }

    /// <inheritdoc />
    protected override async ValueTask<ClientEntity?> TryGetEntityAsync(
        Expression<Func<ClientEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return await DbContext.Clients
            .Include(client => client.Tenant)
            .Include(client => client.Urls)
            .Include(client => client.Secrets)
            .ThenInclude(clientSecret => clientSecret.Secret)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedClient persistedClient,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantAsync(persistedClient, cancellationToken);

        persistedClient.Id = NextId(persistedClient.Id);

        var urls = new List<ClientUrlEntity>(persistedClient.RedirectUrls.Count);
        var secrets = new List<ClientSecretEntity>(persistedClient.SecretsState.Value.Count);

        var clientEntity = new ClientEntity
        {
            Id = persistedClient.Id,
            TenantId = tenantEntity.Id,
            ClientId = persistedClient.ClientId,
            NormalizedClientId = Normalize(persistedClient.ClientId),
            ConcurrencyToken = NextConcurrencyToken(),
            SettingsConcurrencyToken = persistedClient.SettingsState.ConcurrencyToken,
            SecretsConcurrencyToken = persistedClient.SecretsState.ConcurrencyToken,
            IsDisabled = persistedClient.IsDisabled,
            SettingsJson = persistedClient.SettingsState.Value,
            Tenant = tenantEntity,
            Urls = urls,
            Secrets = secrets
        };

        foreach (var url in persistedClient.RedirectUrls)
        {
            var clientUrlEntity = new ClientUrlEntity
            {
                Id = NextId(),
                TenantId = tenantEntity.Id,
                ClientId = clientEntity.Id,
                UrlType = UrlTypes.RedirectUrl,
                UrlValue = url,
                Tenant = tenantEntity,
                Client = clientEntity
            };

            urls.Add(clientUrlEntity);

            await DbContext.ClientUrls.AddAsync(clientUrlEntity, cancellationToken);
        }

        foreach (var persistedSecret in persistedClient.SecretsState.Value)
        {
            var secretEntity = MapNew(persistedSecret);

            await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

            var clientSecretEntity = new ClientSecretEntity
            {
                Id = NextId(),

                Tenant = tenantEntity,
                TenantId = tenantEntity.Id,

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
        CancellationToken cancellationToken
    )
    {
        var clientEntity = await GetEntityByIdAsync(persistedClient.Id, cancellationToken);

        clientEntity.ConcurrencyToken = NextConcurrencyToken();
        clientEntity.SettingsConcurrencyToken = persistedClient.SettingsState.ConcurrencyToken;
        // we don't update the secrets concurrency token here because that is a disconnected entity collection
        clientEntity.IsDisabled = persistedClient.IsDisabled;
        clientEntity.SettingsJson = persistedClient.SettingsState.Value;
    }

    /// <inheritdoc />
    public override async ValueTask RemoveByIdAsync(
        long id,
        CancellationToken cancellationToken
    )
    {
        var client = await TryGetEntityAsync(client => client.Id == id, cancellationToken);
        if (client is null)
            return;

        var tenantId = client.TenantId;

        DbContext.ClientUrls.RemoveRange(
            client.Urls.Where(url =>
                url.TenantId == tenantId &&
                url.ClientId == id
            )
        );

        var clientSecrets = DbContext.ClientSecrets.Where(clientSecret =>
            clientSecret.TenantId == tenantId &&
            clientSecret.ClientId == id
        );

        DbContext.Secrets.RemoveRange(
            clientSecrets.Select(clientSecret => clientSecret.Secret)
        );

        DbContext.ClientSecrets.RemoveRange(clientSecrets);

        DbContext.Clients.Remove(client);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedClient?> TryGetByClientIdAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);
        var normalizedClientId = Normalize(clientId);

        return await TryGetAsync(
            client =>
                client.Tenant.NormalizedTenantId == normalizedTenantId &&
                client.NormalizedClientId == normalizedClientId,
            cancellationToken
        );
    }
}
