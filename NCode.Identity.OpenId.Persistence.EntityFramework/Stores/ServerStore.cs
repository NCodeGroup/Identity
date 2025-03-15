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
using System.Text.Json;
using IdGen;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IServerStore"/> that uses Entity Framework Core for persistence.
/// </summary>
[PublicAPI]
public class ServerStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStoreWithEntityId<PersistedServer, ServerEntity>, IServerStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    protected override ValueTask<PersistedServer> MapAsync(
        ServerEntity entity,
        CancellationToken cancellationToken
    ) =>
        ValueTask.FromResult(new PersistedServer
        {
            Id = entity.Id,
            ServerId = entity.ServerId,
            SecretsState = ConcurrentStateFactory.Create(MapExisting(entity.Secrets), entity.SecretsConcurrencyToken),
            SettingsState = ConcurrentStateFactory.Create(entity.SettingsJson, entity.SettingsConcurrencyToken),
        });

    /// <inheritdoc />
    protected override async ValueTask<ServerEntity?> TryGetEntityAsync(
        Expression<Func<ServerEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return await DbContext.Servers
            .Include(server => server.Secrets)
            .ThenInclude(serverSecret => serverSecret.Secret)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedServer persistedServer,
        CancellationToken cancellationToken
    )
    {
        var serverId = NextId(persistedServer.Id);
        var secrets = new List<ServerSecretEntity>(persistedServer.SecretsState.Value.Count);

        var serverEntity = new ServerEntity
        {
            Id = serverId,
            ServerId = persistedServer.ServerId,
            NormalizedServerId = Normalize(persistedServer.ServerId),
            ConcurrencyToken = NextConcurrencyToken(),
            SettingsConcurrencyToken = persistedServer.SettingsState.ConcurrencyToken,
            SecretsConcurrencyToken = persistedServer.SecretsState.ConcurrencyToken,
            SettingsJson = persistedServer.SettingsState.Value,
            Secrets = secrets
        };

        foreach (var persistedSecret in persistedServer.SecretsState.Value)
        {
            var secretEntity = MapNew(persistedSecret);

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
    public async ValueTask<PersistedServer?> TryGetByServerIdAsync(
        string serverId,
        CancellationToken cancellationToken
    )
    {
        var normalizedServerId = Normalize(serverId);

        return await TryGetAsync(
            server => server.NormalizedServerId == normalizedServerId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<ConcurrentState<JsonElement>> GetSettingsAsync(
        string serverId,
        ConcurrentState<JsonElement> lastKnownState,
        CancellationToken cancellationToken
    )
    {
        var normalizedServerId = Normalize(serverId);

        var entity = await DbContext.Servers
            .Where(server => server.NormalizedServerId == normalizedServerId)
            .SingleAsync(cancellationToken);

        var concurrencyToken = entity.SettingsConcurrencyToken;
        if (string.Equals(concurrencyToken, lastKnownState.ConcurrencyToken, StringComparison.Ordinal))
        {
            return lastKnownState;
        }

        var settingsJson = entity.SettingsJson;
        return ConcurrentStateFactory.Create(settingsJson, concurrencyToken);
    }

    /// <inheritdoc />
    public async ValueTask<ConcurrentState<IReadOnlyCollection<PersistedSecret>>> GetSecretsAsync(
        string serverId,
        ConcurrentState<IReadOnlyCollection<PersistedSecret>> lastKnownState,
        CancellationToken cancellationToken
    )
    {
        var normalizedServerId = Normalize(serverId);

        var concurrencyToken = await DbContext.Servers
            .Where(server => server.NormalizedServerId == normalizedServerId)
            .Select(server => server.SecretsConcurrencyToken)
            .SingleAsync(cancellationToken);

        if (string.Equals(concurrencyToken, lastKnownState.ConcurrencyToken, StringComparison.Ordinal))
        {
            return lastKnownState;
        }

        IReadOnlyCollection<PersistedSecret> secrets = await DbContext.ServerSecrets
            .Include(serverSecret => serverSecret.Server)
            .Include(serverSecret => serverSecret.Secret)
            .Where(serverSecret => serverSecret.Server.NormalizedServerId == normalizedServerId)
            .Select(serverSecret => serverSecret.Secret)
            .Select(secret => MapExisting(secret))
            .ToListAsync(cancellationToken);

        return ConcurrentStateFactory.Create(secrets, concurrencyToken);
    }
}
