// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides a default implementation of the <see cref="ITeamSettingsReconciler"/> abstraction. It reads the team's
/// row, resolves the team's effective settings from the row's committed document and this replica's configuration,
/// and compares their fingerprint with the one recorded as reconciled. On a difference it takes a lease in the
/// <see cref="IExpiringKeySet"/> that names the row's concurrency token and the fingerprint, so one replica reconciles
/// each change, sends a <see cref="ReconcileTeamSettingsCommand"/> that carries the settings recorded as reconciled
/// (none when the record is missing or cannot be read), and records the fingerprint and the settings it reconciled
/// while the row is unchanged. A lease that is held, a pass that fails, and a record that loses a race with a change
/// to the row leave the settings unreconciled, so the caller tries again.
/// </summary>
// See ADR-1411 and ADR-1412.
internal sealed class DefaultTeamSettingsReconciler(
    IStoreManagerFactory storeManagerFactory,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    ISettingSerializer settingSerializer,
    ISettingCollectionFactory settingCollectionFactory,
    IExpiringKeySet expiringKeySet,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OpenIdOptions> optionsAccessor,
    TimeProvider timeProvider,
    ILogger<DefaultTeamSettingsReconciler> logger
) : ITeamSettingsReconciler
{
    /// <summary>
    /// Contains the purpose that prefixes the key of the reconciliation lease.
    /// </summary>
    internal const string LeasePurpose = "team-settings-reconciliation";

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
    private ILogger<DefaultTeamSettingsReconciler> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private OpenIdOptions Options => OptionsAccessor.Value;

    /// <inheritdoc />
    public async ValueTask<bool> ReconcileAsync(string teamId, CancellationToken cancellationToken)
    {
        var persistedTeam = await GetTeamOrDefaultAsync(teamId, cancellationToken);
        if (persistedTeam is null)
            return true;

        // The committed document and this replica's configuration, read from one row so the token matches the document.
        var settings = await EffectiveSettingsResolver.ResolveTeamAsync(
            teamId,
            persistedTeam.Settings.Value,
            cancellationToken
        );
        var jsonOptions = OpenIdEnvironmentProvider.Get().JsonSerializerOptions;
        var fingerprint = SettingsFingerprint.Compute(settings, jsonOptions);
        if (
            string.Equals(
                persistedTeam.ReconciledSettingsFingerprint,
                fingerprint,
                StringComparison.Ordinal
            )
        )
            return true;

        // Replicas that observe one change share the key; every write to the row changes the token (ADR-1412).
        var leaseKey = $"{LeasePurpose}:{teamId}:{persistedTeam.ConcurrencyToken}:{fingerprint}";
        var leaseExpiresWhen = TimeProvider.GetUtcNow() + Options.Team.ReconciliationLeaseDuration;
        if (!await ExpiringKeySet.TryAddAsync(leaseKey, leaseExpiresWhen, cancellationToken))
            return false;

        var previousSettings = ReadReconciledSettings(persistedTeam, jsonOptions);
        Logger.TeamSettingsReconciliationStarted(teamId, fingerprint);
        var command = new ReconcileTeamSettingsCommand(teamId, settings)
        {
            PreviousSettings = previousSettings,
        };
        await SendAsync(command, cancellationToken);

        persistedTeam.ReconciledSettingsFingerprint = fingerprint;
        persistedTeam.ReconciledSettings = SettingsSnapshot.Write(settings, jsonOptions);
        if (!await TryRecordAsync(persistedTeam, cancellationToken))
            return false;

        Logger.TeamSettingsReconciled(teamId, fingerprint);
        return true;
    }

    // A record that cannot be read leaves nothing to compare a change with, so every tenant and client is reconciled.
    private IReadOnlySettingCollection? ReadReconciledSettings(
        PersistedTeam persistedTeam,
        JsonSerializerOptions jsonOptions
    )
    {
        if (persistedTeam.ReconciledSettings is not { } json)
            return null;

        try
        {
            var settings = SettingSerializer.DeserializeSettings(
                json,
                OpenIdSettingScopes.Team,
                jsonOptions
            );
            return SettingCollectionFactory.Create(settings);
        }
        catch (JsonException exception)
        {
            Logger.ReconciledTeamSettingsUnreadable(persistedTeam.TeamId, exception);
            return null;
        }
    }

    private async ValueTask<PersistedTeam?> GetTeamOrDefaultAsync(
        string teamId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();
        return await store.GetOrDefaultAsync(teamId, cancellationToken);
    }

    // The mediator is scoped (its handlers' unit of work is), and the pass runs outside any request, so the command
    // is sent in its own scope.
    private async ValueTask SendAsync(
        ReconcileTeamSettingsCommand command,
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
        PersistedTeam persistedTeam,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();
        try
        {
            await store.UpdateReconciledSettingsAsync(persistedTeam, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (StoreConcurrencyException)
        {
            return false;
        }
    }
}
