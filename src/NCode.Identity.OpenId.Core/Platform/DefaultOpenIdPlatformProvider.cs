// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdPlatformProvider"/> abstraction. The platform it
/// materializes is registered with <see cref="IMaterializedPlatformRegistry"/>, so background work can wait for the
/// platform and a committed change to its settings can refresh it. Whenever the platform's settings change, by any path,
/// every cached tenant is evicted from <see cref="IOpenIdTenantCache"/> and every cached team from
/// <see cref="IOpenIdTeamCache"/>, because a tenant's and a team's settings derive from the platform's when they are
/// materialized.
/// </summary>
// See ADR-1219 and ADR-1289.
internal class DefaultOpenIdPlatformProvider(
    IOpenIdPlatformFactory factory,
    IMaterializedPlatformRegistry materializedServerRegistry,
    IOpenIdTenantCache tenantCache,
    IOpenIdTeamCache teamCache,
    ILogger<DefaultOpenIdPlatformProvider> logger
) : IOpenIdPlatformProvider, IAsyncDisposable
{
    private IOpenIdPlatformFactory Factory { get; } = factory;
    private IMaterializedPlatformRegistry MaterializedPlatformRegistry { get; } =
        materializedServerRegistry;
    private IOpenIdTenantCache TenantCache { get; } = tenantCache;
    private IOpenIdTeamCache TeamCache { get; } = teamCache;
    private ILogger<DefaultOpenIdPlatformProvider> Logger { get; } = logger;
    private OpenIdPlatform? InstanceOrNull { get; set; }
    private IDisposable? SettingsChangeRegistration { get; set; }
    private SemaphoreSlim SyncRoot { get; } = new(initialCount: 1, maxCount: 1);

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    public async ValueTask DisposeAsync()
    {
        SettingsChangeRegistration?.Dispose();

        switch (InstanceOrNull)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;

            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        SyncRoot.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdPlatform> GetAsync(
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    )
    {
        // ReSharper disable once InvertIf
        if (InstanceOrNull is null)
        {
            await SyncRoot.WaitAsync(cancellationToken);
            try
            {
                if (InstanceOrNull is null)
                {
                    var platform = await Factory.CreateAsync(openIdEnvironment, cancellationToken);
                    InstanceOrNull = platform;

                    // Background work waits for the platform this replica materializes, and a committed change refreshes it (ADR-1219).
                    MaterializedPlatformRegistry.Register(platform);

                    // A tenant computes the profiles its environment type implies from the platform's settings when it is
                    // materialized, so a changed platform reaches cached tenants at once rather than when they expire (ADR-1289).
                    SettingsChangeRegistration = ChangeToken.OnChange(
                        platform.SettingsProvider.GetChangeToken,
                        EvictTenants
                    );
                }
            }
            finally
            {
                SyncRoot.Release();
            }
        }

        return InstanceOrNull;
    }

    private void EvictTenants()
    {
        Observe(
            () => TenantCache.ClearAsync(PropertyBagFactory.Create(), CancellationToken.None),
            Logger.TenantCacheClearFailed
        );
        Observe(
            () => TeamCache.ClearAsync(PropertyBagFactory.Create(), CancellationToken.None),
            Logger.TeamCacheClearFailed
        );
    }

    private static void Observe(Func<ValueTask> clear, Action<Exception> onFailure)
    {
        ValueTask clearing;
        try
        {
            clearing = clear();
        }
        catch (Exception exception)
        {
            onFailure(exception);
            return;
        }

        if (!clearing.IsCompletedSuccessfully)
            _ = ObserveAsync(clearing, onFailure);
    }

    private static async Task ObserveAsync(ValueTask clearing, Action<Exception> onFailure)
    {
        try
        {
            await clearing;
        }
        catch (Exception exception)
        {
            onFailure(exception);
        }
    }
}
