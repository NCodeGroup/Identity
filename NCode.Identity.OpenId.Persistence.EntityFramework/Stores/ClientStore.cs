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
    OpenIdDbContext openIdDbContext
) : BaseStoreWithResourceId<PersistedClient, ClientEntity>, IClientStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <summary>
    /// Maps a <see cref="ClientEntity"/> to a <see cref="PersistedClientSettings"/> instance.
    /// </summary>
    /// <param name="clientEntity">The <see cref="ClientEntity"/> instance to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly mapped <see cref="PersistedClientSettings"/> instance.</returns>
    protected internal virtual ValueTask<PersistedClientSettings> MapSettingsAsync(
        ClientEntity clientEntity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(new PersistedClientSettings
        {
            TenantId = clientEntity.Tenant.TenantId,
            ClientId = clientEntity.ClientId,
            ConcurrencyToken = clientEntity.SettingsConcurrencyToken,
            Value = clientEntity.SettingsJson
        });
    }

    /// <summary>
    /// Maps a <see cref="ClientEntity"/> to a <see cref="PersistedClientSecrets"/> instance.
    /// </summary>
    /// <param name="clientEntity">The <see cref="ClientEntity"/> instance to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly mapped <see cref="PersistedClientSecrets"/> instance.</returns>
    protected internal virtual ValueTask<PersistedClientSecrets> MapSecretsAsync(
        ClientEntity clientEntity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(new PersistedClientSecrets
        {
            TenantId = clientEntity.Tenant.TenantId,
            ClientId = clientEntity.ClientId,
            ConcurrencyToken = clientEntity.SecretsConcurrencyToken,
            Value = MapToPersistedSecrets(clientEntity.Secrets),
        });
    }

    /// <inheritdoc />
    protected override async ValueTask<PersistedClient> MapFromEntityAsync(
        ClientEntity client,
        CancellationToken cancellationToken
    )
    {
        return new PersistedClient
        {
            TenantId = client.Tenant.TenantId,
            ClientId = client.ClientId,
            ConcurrencyToken = client.ConcurrencyToken,
            IsDisabled = client.IsDisabled,
            Settings = await MapSettingsAsync(client, cancellationToken),
            Secrets = await MapSecretsAsync(client, cancellationToken)
        };
    }

    /// <inheritdoc />
    protected override async ValueTask<ClientEntity?> GetEntityOrDefaultAsync(
        Expression<Func<ClientEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return await DbContext.Clients
            .Include(client => client.Tenant)
            .Include(client => client.Secrets)
            .ThenInclude(clientSecret => clientSecret.Secret)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<ClientEntity?> GetEntityOrDefaultAsync(
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var normalizedClientId = Normalize(clientId);
        return await GetEntityOrDefaultAsync(
            entity => entity.NormalizedClientId == normalizedClientId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedClient persistedClient,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantEntityAsync(persistedClient, cancellationToken);

        persistedClient.ConcurrencyToken = NextConcurrencyToken();
        persistedClient.Settings.ConcurrencyToken = NextConcurrencyToken();
        persistedClient.Secrets.ConcurrencyToken = NextConcurrencyToken();

        var secrets = new List<ClientSecretEntity>(persistedClient.Secrets.Value.Count);

        var clientEntity = new ClientEntity
        {
            Id = NextId(),
            TenantId = tenantEntity.Id,
            ClientId = persistedClient.ClientId,
            NormalizedClientId = Normalize(persistedClient.ClientId),
            ConcurrencyToken = persistedClient.ConcurrencyToken,
            SettingsConcurrencyToken = persistedClient.Settings.ConcurrencyToken,
            SecretsConcurrencyToken = persistedClient.Secrets.ConcurrencyToken,
            IsDisabled = persistedClient.IsDisabled,
            SettingsJson = persistedClient.Settings.Value,
            Tenant = tenantEntity,
            Secrets = secrets
        };

        foreach (var persistedSecret in persistedClient.Secrets.Value)
        {
            persistedSecret.ConcurrencyToken = NextConcurrencyToken();

            var secretEntity = MapToSecretEntity(persistedSecret);

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
    public override async ValueTask UpdateAsync(
        PersistedClient persistedClient,
        CancellationToken cancellationToken
    )
    {
        var clientId = persistedClient.ClientId;
        var clientEntity = await GetEntityAsync(clientId, cancellationToken);

        if (!string.Equals(persistedClient.ConcurrencyToken, clientEntity.ConcurrencyToken, StringComparison.Ordinal))
        {
            throw new DbUpdateConcurrencyException(
                $"The OpenId Client with ClientId='{clientId}' has been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        clientEntity.ConcurrencyToken = nextConcurrencyToken;
        clientEntity.IsDisabled = persistedClient.IsDisabled;

        DbContext.Clients.Update(clientEntity);

        persistedClient.ConcurrencyToken = nextConcurrencyToken;
    }

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(
        PersistedClientSettings persistedClientSettings,
        CancellationToken cancellationToken
    )
    {
        var clientId = persistedClientSettings.ClientId;
        var clientEntity = await GetEntityAsync(clientId, cancellationToken);

        if (!string.Equals(persistedClientSettings.ConcurrencyToken, clientEntity.SettingsConcurrencyToken, StringComparison.Ordinal))
        {
            throw new DbUpdateConcurrencyException(
                $"The settings for OpenId Client with ClientId='{clientId}' have been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        clientEntity.SettingsConcurrencyToken = nextConcurrencyToken;
        clientEntity.SettingsJson = persistedClientSettings.Value;

        DbContext.Clients.Update(clientEntity);

        persistedClientSettings.ConcurrencyToken = nextConcurrencyToken;
    }
}
