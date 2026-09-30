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
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.ResourceServers;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Core.ResourceServers;

/// <summary>
/// Provides a default implementation of <see cref="ISystemResourceServerSeeder"/> that seeds every registered
/// <see cref="ISystemResourceServerProvider"/> into a tenant.
/// </summary>
internal class DefaultSystemResourceServerSeeder(
    IStoreManagerFactory storeManagerFactory,
    ICryptoService cryptoService,
    IEnumerable<ISystemResourceServerProvider> providers
) : ISystemResourceServerSeeder
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ICryptoService CryptoService { get; } = cryptoService;
    private ImmutableArray<ISystemResourceServerProvider> Providers { get; } = [.. providers];

    /// <inheritdoc />
    public async ValueTask SeedAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (Providers.Length == 0)
        {
            return;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        foreach (var provider in Providers)
        {
            var descriptor = provider.GetDescriptor();

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
