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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdServerProvider"/> abstraction. The server it
/// materializes is registered with <see cref="IMaterializedServerRegistry"/>, so background work can wait for the
/// server and a committed change to its settings can refresh it. Whenever the server's settings change, by any path,
/// every cached tenant is evicted from <see cref="IOpenIdTenantCache"/>, because a tenant's settings derive from the
/// server's when it is materialized.
/// </summary>
// See ADR-0219 and ADR-0289.
internal class DefaultOpenIdServerProvider(
    IOpenIdServerFactory factory,
    IMaterializedServerRegistry materializedServerRegistry,
    IOpenIdTenantCache tenantCache,
    ILogger<DefaultOpenIdServerProvider> logger
) : IOpenIdServerProvider, IAsyncDisposable
{
    private IOpenIdServerFactory Factory { get; } = factory;
    private IMaterializedServerRegistry MaterializedServerRegistry { get; } =
        materializedServerRegistry;
    private IOpenIdTenantCache TenantCache { get; } = tenantCache;
    private ILogger<DefaultOpenIdServerProvider> Logger { get; } = logger;
    private OpenIdServer? InstanceOrNull { get; set; }
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
    public async ValueTask<OpenIdServer> GetAsync(
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
                    var server = await Factory.CreateAsync(openIdEnvironment, cancellationToken);
                    InstanceOrNull = server;

                    // Background work waits for the server this replica materializes, and a committed change refreshes it (ADR-0219).
                    MaterializedServerRegistry.Register(server);

                    // A tenant computes the profiles its environment type implies from the server's settings when it is
                    // materialized, so a changed server reaches cached tenants at once rather than when they expire (ADR-0289).
                    SettingsChangeRegistration = ChangeToken.OnChange(
                        server.SettingsProvider.GetChangeToken,
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
        ValueTask clearing;
        try
        {
            clearing = TenantCache.ClearAsync(PropertyBagFactory.Create(), CancellationToken.None);
        }
        catch (Exception exception)
        {
            Logger.TenantCacheClearFailed(exception);
            return;
        }

        if (!clearing.IsCompletedSuccessfully)
            _ = ObserveAsync(clearing);
    }

    private async Task ObserveAsync(ValueTask clearing)
    {
        try
        {
            await clearing;
        }
        catch (Exception exception)
        {
            Logger.TenantCacheClearFailed(exception);
        }
    }
}
