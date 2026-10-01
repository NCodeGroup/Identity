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
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IClientStore"/> that uses Entity Framework Core for persistence.
/// </summary>
internal class ClientStore(
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
        return ValueTask.FromResult(
            new PersistedClientSettings
            {
                TenantId = clientEntity.Tenant.TenantId,
                ClientId = clientEntity.ClientId,
                ConcurrencyToken = clientEntity.SettingsConcurrencyToken,
                Value = clientEntity.SettingsJson,
            }
        );
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
        return ValueTask.FromResult(
            new PersistedClientSecrets
            {
                TenantId = clientEntity.Tenant.TenantId,
                ClientId = clientEntity.ClientId,
                ConcurrencyToken = clientEntity.SecretsConcurrencyToken,
                Value = MapToPersistedSecrets(clientEntity.Secrets),
            }
        );
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
            Secrets = await MapSecretsAsync(client, cancellationToken),
        };
    }

    /// <inheritdoc />
    protected override async ValueTask<ClientEntity?> GetEntityOrDefaultAsync(
        Expression<Func<ClientEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext
                .Clients.Include(client => client.Tenant)
                .Include(client => client.Secrets)
                    .ThenInclude(clientSecret => clientSecret.Secret)
                .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<ClientEntity?> GetEntityOrDefaultAsync(
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var normalizedClientId = Normalize(resourceId);
        return await GetEntityOrDefaultAsync(
            entity => entity.NormalizedClientId == normalizedClientId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<ClientEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext
            .Clients.Include(client => client.Tenant)
            .Include(client => client.Secrets)
                .ThenInclude(clientSecret => clientSecret.Secret)
            .AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(client => client.Id > id);
        }

        return await query.OrderBy(client => client.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(ClientEntity entity) => entity.Id;

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedClient persistedClient,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantEntityAsync(persistedClient, cancellationToken);

        // Assign the row token up front so create can return it without a re-read; the interceptor leaves a
        // pre-seeded insert token intact (ADR-0012). The sub-resource version columns stay manual.
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
            Secrets = secrets,
        };

        foreach (var persistedSecret in persistedClient.Secrets.Value)
        {
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

        if (
            !string.Equals(
                persistedClient.ConcurrencyToken,
                clientEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new DbUpdateConcurrencyException(
                $"The OpenId Client with ClientId='{clientId}' has been modified by another process. Please reload and try again."
            );
        }

        // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-0012).
        clientEntity.IsDisabled = persistedClient.IsDisabled;

        DbContext.Clients.Update(clientEntity);
    }

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(
        PersistedClientSettings persistedClientSettings,
        CancellationToken cancellationToken
    )
    {
        var clientId = persistedClientSettings.ClientId;
        var clientEntity = await GetEntityAsync(clientId, cancellationToken);

        if (
            !string.Equals(
                persistedClientSettings.ConcurrencyToken,
                clientEntity.SettingsConcurrencyToken,
                StringComparison.Ordinal
            )
        )
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

    /// <inheritdoc />
    public async ValueTask<PersistedClientSecrets?> GetSecretsOrDefaultAsync(
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var clientEntity = await GetEntityOrDefaultAsync(clientId, cancellationToken);
        if (clientEntity is null)
        {
            return null;
        }

        return await MapSecretsAsync(clientEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedClientSecret?> GetSecretOrDefaultAsync(
        string clientId,
        string secretId,
        CancellationToken cancellationToken
    )
    {
        var normalizedClientId = Normalize(clientId);
        var normalizedSecretId = Normalize(secretId);

        var row = await DbContext
            .ClientSecrets.Where(clientSecret =>
                clientSecret.Client.NormalizedClientId == normalizedClientId
                && clientSecret.Secret.NormalizedSecretId == normalizedSecretId
            )
            .Select(clientSecret => new
            {
                clientSecret.Tenant.TenantId,
                clientSecret.Client.ClientId,
                clientSecret.Secret,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new PersistedClientSecret
        {
            TenantId = row.TenantId,
            ClientId = row.ClientId,
            Value = MapToPersistedSecret(row.Secret),
        };
    }

    /// <inheritdoc />
    public async ValueTask AddSecretAsync(
        string clientId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    )
    {
        var clientEntity = await GetEntityAsync(clientId, cancellationToken);

        var normalizedSecretId = Normalize(persistedSecret.SecretId);
        var alreadyExists = clientEntity.Secrets.Any(clientSecret =>
            clientSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (alreadyExists)
        {
            throw new InvalidOperationException(
                $"A secret with SecretId='{persistedSecret.SecretId}' already exists for OpenId Client with ClientId='{clientId}'."
            );
        }

        // A created secret cannot have a concurrency conflict, so assign its token up front and return it on
        // the DTO; the interceptor leaves a pre-seeded insert token intact (ADR-0012).
        persistedSecret.ConcurrencyToken = NextConcurrencyToken();
        var secretEntity = MapToSecretEntity(persistedSecret);
        await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

        var clientSecretEntity = new ClientSecretEntity
        {
            Id = NextId(),
            Tenant = clientEntity.Tenant,
            TenantId = clientEntity.TenantId,
            Client = clientEntity,
            ClientId = clientEntity.Id,
            Secret = secretEntity,
            SecretId = secretEntity.Id,
        };
        await DbContext.ClientSecrets.AddAsync(clientSecretEntity, cancellationToken);

        // The client is tracked; mutating the token marks it Modified via change detection.
        clientEntity.SecretsConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask UpdateSecretAsync(
        string clientId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    )
    {
        var clientEntity = await GetEntityAsync(clientId, cancellationToken);

        var normalizedSecretId = Normalize(persistedSecret.SecretId);
        var clientSecretEntity = clientEntity.Secrets.SingleOrDefault(clientSecret =>
            clientSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (clientSecretEntity is null)
        {
            throw new InvalidOperationException(
                $"A secret with SecretId='{persistedSecret.SecretId}' was not found for OpenId Client with ClientId='{clientId}'."
            );
        }

        var secretEntity = clientSecretEntity.Secret;
        if (
            !string.Equals(
                persistedSecret.ConcurrencyToken,
                secretEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new DbUpdateConcurrencyException(
                $"The secret with SecretId='{persistedSecret.SecretId}' has been modified by another process. Please reload and try again."
            );
        }

        // Only metadata is mutable; key material is immutable once generated (see ADR-0011). The secret's
        // row-level ConcurrencyToken is regenerated by the interceptor on save (ADR-0012).
        secretEntity.Use = persistedSecret.Use;
        secretEntity.Algorithm = persistedSecret.Algorithm;
        secretEntity.ExpiresWhen = persistedSecret.ExpiresWhen.ToUniversalTime();

        clientEntity.SecretsConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveSecretAsync(
        string clientId,
        string secretId,
        CancellationToken cancellationToken
    )
    {
        var clientEntity = await GetEntityAsync(clientId, cancellationToken);

        var normalizedSecretId = Normalize(secretId);
        var clientSecretEntity = clientEntity.Secrets.SingleOrDefault(clientSecret =>
            clientSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (clientSecretEntity is null)
        {
            return false;
        }

        DbContext.ClientSecrets.Remove(clientSecretEntity);
        DbContext.Secrets.Remove(clientSecretEntity.Secret);

        // The client is tracked; mutating the token marks it Modified via change detection.
        clientEntity.SecretsConcurrencyToken = NextConcurrencyToken();

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(string clientId, CancellationToken cancellationToken)
    {
        var clientEntity = await GetEntityOrDefaultAsync(clientId, cancellationToken);
        if (clientEntity is null)
        {
            return false;
        }

        var hasSecrets = await DbContext.ClientSecrets.AnyAsync(
            clientSecret => clientSecret.ClientId == clientEntity.Id,
            cancellationToken
        );
        if (hasSecrets)
        {
            throw new InvalidOperationException(
                $"The OpenId Client with ClientId='{clientId}' cannot be removed because it still has one or more secrets."
            );
        }

        DbContext.Clients.Remove(clientEntity);

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasDependentsAsync(
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var clientEntity = await GetEntityOrDefaultAsync(clientId, cancellationToken);
        if (clientEntity is null)
        {
            return false;
        }

        return await DbContext.ClientSecrets.AnyAsync(
            clientSecret => clientSecret.ClientId == clientEntity.Id,
            cancellationToken
        );
    }
}
