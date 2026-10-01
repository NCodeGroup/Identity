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

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.ResourceServers;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Core.ResourceServers;

/// <summary>
/// Provides a default implementation of <see cref="ISystemResourceServerSeeder"/> that seeds every registered
/// <see cref="ISystemResourceServerProvider"/> whose <see cref="SystemResourceServerDescriptor.Plane"/> applies to
/// the tenant being seeded. Control-plane providers are seeded only into the root tenant
/// (<see href="../../../docs/adr/0024-control-plane-and-per-tenant-planes.md">ADR-0024</see>).
/// </summary>
internal class DefaultSystemResourceServerSeeder(
    IStoreManagerFactory storeManagerFactory,
    ICryptoService cryptoService,
    IOptions<TenantResolutionOptions> optionsAccessor,
    IEnumerable<ISystemResourceServerProvider> providers
) : ISystemResourceServerSeeder
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IOptions<TenantResolutionOptions> OptionsAccessor { get; } = optionsAccessor;
    private ImmutableArray<ISystemResourceServerProvider> Providers { get; } = [.. providers];

    /// <inheritdoc />
    public async ValueTask SeedAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (Providers.Length == 0)
        {
            return;
        }

        var isRootTenant = string.Equals(
            tenantId,
            OptionsAccessor.Value.RootTenantId,
            StringComparison.Ordinal
        );

        var applicable = Providers
            .Select(provider => provider.GetDescriptor())
            .Where(descriptor =>
                descriptor.Plane != SystemResourceServerPlane.Control || isRootTenant
            )
            .ToList();

        if (applicable.Count == 0)
        {
            return;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        foreach (var descriptor in applicable)
        {
            var resourceServer = new PersistedResourceServer
            {
                TenantId = tenantId,
                ResourceServerId = CryptoService.GenerateResourceId(),
                Identifier = descriptor.Identifier,
                ConcurrencyToken = string.Empty,
                Name = descriptor.Name,
                IsSystem = true,
                IsDisabled = false,
                Settings = JsonSerializer.SerializeToElement(null, typeof(object)),
                Scopes = descriptor
                    .Scopes.Select(scope => new PersistedScope
                    {
                        Value = scope.Value,
                        Description = scope.Description,
                        IsSystem = true,
                    })
                    .ToList(),
            };

            await store.AddAsync(resourceServer, cancellationToken);
        }

        await storeManager.SaveChangesAsync(cancellationToken);
    }
}
