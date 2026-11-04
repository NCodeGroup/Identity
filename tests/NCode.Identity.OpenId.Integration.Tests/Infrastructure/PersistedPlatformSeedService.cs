// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// Persists the server's own settings document before any hosted service starts, so the server materializes with it.
/// The host runs every <see cref="StartingAsync"/> before any <see cref="StartAsync"/>, so the seed precedes the
/// service that materializes the server when the host starts, whatever the order they are registered in.
/// </summary>
/// <param name="scopeFactory">The <see cref="IServiceScopeFactory"/> that creates the scope the seed runs in.</param>
/// <param name="settings">The server's settings document.</param>
internal sealed class PersistedPlatformSeedService(
    IServiceScopeFactory scopeFactory,
    IReadOnlyDictionary<string, object> settings
) : IHostedLifecycleService
{
    private IServiceScopeFactory ScopeFactory { get; } = scopeFactory;

    private IReadOnlyDictionary<string, object> Settings { get; } = settings;

    /// <inheritdoc />
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        const string platformId = OpenIdPlatformOptions.DefaultPlatformId;

        await using var scope = ScopeFactory.CreateAsyncScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        if (await store.GetOrDefaultAsync(platformId, cancellationToken) is not null)
        {
            throw new InvalidOperationException(
                "The server is already persisted, so it may already be materialized with its earlier settings."
            );
        }

        var persistedPlatform = new PersistedPlatform
        {
            PlatformId = platformId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            DisplayName = platformId,
            Settings = new PersistedPlatformSettings
            {
                PlatformId = platformId,
                ConcurrencyToken = Guid.NewGuid().ToString("N"),
                Value = System.Text.Json.JsonSerializer.SerializeToElement(Settings),
            },
            Secrets = new PersistedPlatformSecrets
            {
                PlatformId = platformId,
                ConcurrencyToken = Guid.NewGuid().ToString("N"),
                Value = [],
            },
        };

        await store.AddAsync(persistedPlatform, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
