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
/// the tenant being seeded. Control-plane providers are seeded only into the root tenant.
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

        // Multiple providers may contribute scopes to the same system resource server (for example, the tenant-plane
        // and control-plane families of the management resource server); merge them into one row per identifier.
        foreach (var group in applicable.GroupBy(descriptor => descriptor.Identifier))
        {
            var scopes = group
                .SelectMany(descriptor => descriptor.Scopes)
                .GroupBy(scope => scope.Value, StringComparer.Ordinal)
                .Select(byValue => byValue.First())
                .Select(scope => new PersistedScope
                {
                    Value = scope.Value,
                    Description = scope.Description,
                    IsSystem = true,
                })
                .ToList();

            var resourceServer = new PersistedResourceServer
            {
                TenantId = tenantId,
                ResourceServerId = CryptoService.GenerateResourceId(),
                Identifier = group.Key,
                ConcurrencyToken = string.Empty,
                Name = group.First().Name,
                IsSystem = true,
                IsDisabled = false,
                Settings = JsonSerializer.SerializeToElement(null, typeof(object)),
                Scopes = scopes,
            };

            await store.AddAsync(resourceServer, cancellationToken);
        }

        await storeManager.SaveChangesAsync(cancellationToken);
    }
}
