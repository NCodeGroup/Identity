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

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="IServerSettingsReconciler"/> abstraction. It reads the server's
/// row, resolves the server's effective settings from the row's committed document and this replica's configuration,
/// and compares their fingerprint with the one recorded as reconciled. On a difference it takes a lease in the
/// <see cref="IExpiringKeySet"/> that names the row's concurrency token and the fingerprint, so one replica reconciles
/// each change, sends a <see cref="ReconcileServerSettingsCommand"/> that carries the settings recorded as reconciled
/// (none when the record is missing or cannot be read), and records the fingerprint and the settings it reconciled
/// while the row is unchanged. A lease that is held, a pass that fails, and a record that loses a race with a change
/// to the row leave the settings unreconciled, so the caller tries again.
/// </summary>
// See ADR-0219.
internal sealed class DefaultServerSettingsReconciler(
    IStoreManagerFactory storeManagerFactory,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    ISettingSerializer settingSerializer,
    ISettingCollectionFactory settingCollectionFactory,
    IExpiringKeySet expiringKeySet,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OpenIdOptions> optionsAccessor,
    TimeProvider timeProvider,
    ILogger<DefaultServerSettingsReconciler> logger
) : IServerSettingsReconciler
{
    /// <summary>
    /// Contains the purpose that prefixes the key of the reconciliation lease.
    /// </summary>
    internal const string LeasePurpose = "server-settings-reconciliation";

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private ISettingCollectionFactory SettingCollectionFactory { get; } = settingCollectionFactory;
    private IExpiringKeySet ExpiringKeySet { get; } = expiringKeySet;
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<DefaultServerSettingsReconciler> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private OpenIdOptions Options => OptionsAccessor.Value;

    /// <inheritdoc />
    public async ValueTask<bool> ReconcileAsync(
        string serverId,
        CancellationToken cancellationToken
    )
    {
        var persistedServer = await GetServerOrDefaultAsync(serverId, cancellationToken);
        if (persistedServer is null)
            return true;

        // The committed document and this replica's configuration, read from one row so the token matches the document.
        var settings = await EffectiveSettingsResolver.ResolveServerAsync(
            persistedServer.Settings.Value,
            cancellationToken
        );
        var jsonOptions = OpenIdEnvironmentProvider.Get().JsonSerializerOptions;
        var fingerprint = ServerSettingsFingerprint.Compute(settings, jsonOptions);
        if (
            string.Equals(
                persistedServer.ReconciledSettingsFingerprint,
                fingerprint,
                StringComparison.Ordinal
            )
        )
            return true;

        // Replicas that observe one change share the key; every write to the row changes the token (ADR-0219).
        var leaseKey =
            $"{LeasePurpose}:{serverId}:{persistedServer.ConcurrencyToken}:{fingerprint}";
        var leaseExpiresWhen =
            TimeProvider.GetUtcNow() + Options.Server.ReconciliationLeaseDuration;
        if (!await ExpiringKeySet.TryAddAsync(leaseKey, leaseExpiresWhen, cancellationToken))
            return false;

        var previousSettings = ReadReconciledSettings(persistedServer, jsonOptions);
        Logger.ServerSettingsReconciliationStarted(serverId, fingerprint);
        var command = new ReconcileServerSettingsCommand(serverId, settings)
        {
            PreviousSettings = previousSettings,
        };
        await SendAsync(command, cancellationToken);

        persistedServer.ReconciledSettingsFingerprint = fingerprint;
        persistedServer.ReconciledSettings = ServerSettingsSnapshot.Write(settings, jsonOptions);
        if (!await TryRecordAsync(persistedServer, cancellationToken))
            return false;

        Logger.ServerSettingsReconciled(serverId, fingerprint);
        return true;
    }

    // A record that cannot be read leaves nothing to compare a change with, so every tenant and client is reconciled.
    private IReadOnlySettingCollection? ReadReconciledSettings(
        PersistedServer persistedServer,
        JsonSerializerOptions jsonOptions
    )
    {
        if (persistedServer.ReconciledSettings is not { } json)
            return null;

        try
        {
            var settings = SettingSerializer.DeserializeSettings(
                json,
                OpenIdSettingScopes.Server,
                jsonOptions
            );
            return SettingCollectionFactory.Create(settings);
        }
        catch (JsonException exception)
        {
            Logger.ReconciledServerSettingsUnreadable(persistedServer.ServerId, exception);
            return null;
        }
    }

    private async ValueTask<PersistedServer?> GetServerOrDefaultAsync(
        string serverId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();
        return await store.GetOrDefaultAsync(serverId, cancellationToken);
    }

    // The mediator is scoped (its handlers' unit of work is), and the pass runs outside any request, so the command
    // is sent in its own scope.
    private async ValueTask SendAsync(
        ReconcileServerSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.SendAsync(command, cancellationToken);
    }

    // Records the fingerprint and the settings only while the row is unchanged since it was read; a change during the pass
    // reconciles again.
    private async ValueTask<bool> TryRecordAsync(
        PersistedServer persistedServer,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();
        try
        {
            await store.UpdateReconciledSettingsAsync(persistedServer, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (StoreConcurrencyException)
        {
            return false;
        }
    }
}
