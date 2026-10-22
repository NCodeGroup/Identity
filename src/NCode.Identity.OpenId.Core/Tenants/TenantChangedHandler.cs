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

using Microsoft.Extensions.DependencyInjection;
using NCode.Mediator;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Handles <see cref="TenantChangedCommand"/> by evicting the tenant from this replica's <see cref="IOpenIdTenantCache"/>
/// and, when the tenant's settings changed, seeding it again through the <see cref="ITenantProvisioner"/>. The seed fills
/// only what is missing, so a settings change that permits a new algorithm gets its key at once, and the key reaches the
/// other replicas through their secrets poll.
/// </summary>
// See ADR-0169 (every tenant is provisioned with persisted keys).
internal sealed class TenantChangedHandler(
    IOpenIdTenantCache tenantCache,
    IServiceScopeFactory serviceScopeFactory
) : ICommandHandler<TenantChangedCommand>
{
    private IOpenIdTenantCache TenantCache { get; } = tenantCache;
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        TenantChangedCommand command,
        CancellationToken cancellationToken
    )
    {
        // Evict first: a request that misses the cache from here on materializes the tenant, which seeds it too, so the
        // eviction never serves a state older than the change.
        await TenantCache.RemoveAsync(
            command.TenantId,
            PropertyBagFactory.Create(),
            cancellationToken
        );

        // Only a settings change can make the tenant need a key it lacks. Seeding after any other change would, say,
        // provision again at once the key a secret delete just removed.
        if (!command.SettingsChanged)
        {
            return;
        }

        // The provisioner is scoped (its mediator and unit of work are), and the change may be announced outside a
        // request, so the seed runs in its own scope.
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<ITenantProvisioner>();
        await provisioner.SeedAsync(command.TenantId, cancellationToken);
    }
}
