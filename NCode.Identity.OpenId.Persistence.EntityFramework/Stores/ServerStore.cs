#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
/// Provides a default implementation of <see cref="IServerStore"/> that uses Entity Framework Core for persistence.
/// </summary>
[PublicAPI]
public class ServerStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStoreWithResourceId<PersistedServer, ServerEntity>, IServerStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <summary>
    /// Maps a <see cref="ServerEntity"/> to a <see cref="PersistedServerSettings"/> instance.
    /// </summary>
    /// <param name="serverEntity">The <see cref="ServerEntity"/> instance to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly mapped <see cref="PersistedServerSettings"/> instance.</returns>
    protected internal virtual ValueTask<PersistedServerSettings> MapSettingsAsync(
        ServerEntity serverEntity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(new PersistedServerSettings
        {
            ServerId = serverEntity.ServerId,
            ConcurrencyToken = serverEntity.SettingsConcurrencyToken,
            Value = serverEntity.SettingsJson
        });
    }

    /// <summary>
    /// Maps a <see cref="ServerEntity"/> to a <see cref="PersistedServerSecrets"/> instance.
    /// </summary>
    /// <param name="serverEntity">The <see cref="ServerEntity"/> instance to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly mapped <see cref="PersistedServerSecrets"/> instance.</returns>
    protected internal virtual ValueTask<PersistedServerSecrets> MapSecretsAsync(
        ServerEntity serverEntity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(new PersistedServerSecrets
        {
            ServerId = serverEntity.ServerId,
            ConcurrencyToken = serverEntity.SecretsConcurrencyToken,
            Value = MapToPersistedSecrets(serverEntity.Secrets)
        });
    }

    /// <inheritdoc />
    protected override async ValueTask<PersistedServer> MapFromEntityAsync(
        ServerEntity entity,
        CancellationToken cancellationToken
    )
    {
        return new PersistedServer
        {
            ServerId = entity.ServerId,
            ConcurrencyToken = entity.ConcurrencyToken,
            Settings = await MapSettingsAsync(entity, cancellationToken),
            Secrets = await MapSecretsAsync(entity, cancellationToken)
        };
    }

    /// <inheritdoc />
    protected override async ValueTask<ServerEntity?> GetEntityOrDefaultAsync(
        Expression<Func<ServerEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return await DbContext.Servers
            .Include(server => server.Secrets)
            .ThenInclude(serverSecret => serverSecret.Secret)
            .SingleOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<ServerEntity?> GetEntityOrDefaultAsync(
        string serverId,
        CancellationToken cancellationToken
    )
    {
        var normalizedServerId = Normalize(serverId);
        return await GetEntityOrDefaultAsync(
            entity => entity.NormalizedServerId == normalizedServerId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<PersistedServerSettings?> GetSettingsOrDefaultAsync(
        string serverId,
        CancellationToken cancellationToken
    )
    {
        var serverEntity = await GetEntityOrDefaultAsync(serverId, cancellationToken);
        if (serverEntity is null)
        {
            return null;
        }

        return await MapSettingsAsync(serverEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedServerSecrets> GetSecretsAsync(
        string serverId,
        CancellationToken cancellationToken
    )
    {
        var serverEntity = await GetEntityAsync(serverId, cancellationToken);
        return await MapSecretsAsync(serverEntity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedServer persistedServer,
        CancellationToken cancellationToken
    )
    {
        persistedServer.ConcurrencyToken = NextConcurrencyToken();
        persistedServer.Settings.ConcurrencyToken = NextConcurrencyToken();
        persistedServer.Secrets.ConcurrencyToken = NextConcurrencyToken();

        var secrets = new List<ServerSecretEntity>(persistedServer.Secrets.Value.Count);

        var serverEntity = new ServerEntity
        {
            Id = NextId(),
            ServerId = persistedServer.ServerId,
            NormalizedServerId = Normalize(persistedServer.ServerId),
            ConcurrencyToken = persistedServer.ConcurrencyToken,
            SettingsConcurrencyToken = persistedServer.Settings.ConcurrencyToken,
            SecretsConcurrencyToken = persistedServer.Secrets.ConcurrencyToken,
            SettingsJson = persistedServer.Settings.Value,
            Secrets = secrets
        };

        foreach (var persistedSecret in persistedServer.Secrets.Value)
        {
            persistedSecret.ConcurrencyToken = NextConcurrencyToken();

            var secretEntity = MapToSecretEntity(persistedSecret);

            await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

            var serverSecretEntity = new ServerSecretEntity
            {
                Id = NextId(),

                Server = serverEntity,
                ServerId = serverEntity.Id,

                Secret = secretEntity,
                SecretId = secretEntity.Id,
            };

            secrets.Add(serverSecretEntity);

            await DbContext.ServerSecrets.AddAsync(serverSecretEntity, cancellationToken);
        }

        await DbContext.Servers.AddAsync(serverEntity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask UpdateAsync(
        PersistedServer persistedServer,
        CancellationToken cancellationToken
    )
    {
        var serverId = persistedServer.ServerId;
        var serverEntity = await GetEntityAsync(serverId, cancellationToken);

        if (!string.Equals(persistedServer.ConcurrencyToken, serverEntity.ConcurrencyToken, StringComparison.Ordinal))
        {
            throw new DbUpdateConcurrencyException(
                $"The OpenId Server with ServerId='{serverId}' has been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        serverEntity.ConcurrencyToken = nextConcurrencyToken;

        DbContext.Servers.Update(serverEntity);

        persistedServer.ConcurrencyToken = nextConcurrencyToken;
    }

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(
        PersistedServerSettings persistedServerSettings,
        CancellationToken cancellationToken
    )
    {
        var serverId = persistedServerSettings.ServerId;
        var serverEntity = await GetEntityAsync(serverId, cancellationToken);

        if (!string.Equals(persistedServerSettings.ConcurrencyToken, serverEntity.SettingsConcurrencyToken, StringComparison.Ordinal))
        {
            throw new DbUpdateConcurrencyException(
                $"The settings for OpenId Server with ServerId='{serverId}' have been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        serverEntity.SettingsConcurrencyToken = nextConcurrencyToken;
        serverEntity.SettingsJson = persistedServerSettings.Value;

        DbContext.Servers.Update(serverEntity);

        persistedServerSettings.ConcurrencyToken = nextConcurrencyToken;
    }
}
