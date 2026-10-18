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

using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DeveloperInteractiveSeedHandlerTests : BaseTests
{
    private const string WorkloadTenantId = "default";

    private static DeveloperInteractiveSeedHandler CreateHandler(
        DeveloperInteractiveSeedOptions options
    ) => new(options);

    private SeedTenantCommand CreateCommand(string tenantId, TenantPlane plane)
    {
        // Strict mock: the store manager is untouched on the guard paths under test.
        var mockStoreManager = CreateStrictMock<IStoreManager>();

        var persistedTenant = new PersistedTenant
        {
            TenantId = tenantId,
            Issuer = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = null!,
            Secrets = null!,
        };

        return new SeedTenantCommand(
            new TenantSeedContext
            {
                Tenant = persistedTenant,
                Plane = plane,
                IsNewlyProvisioned = true,
                StoreManager = mockStoreManager.Object,
            }
        );
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNothingConfiguredToSeed_DoesNothing()
    {
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions { SpaClients = [], AdminPassword = null }
        );

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRootPlane_DoesNothing()
    {
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions { AdminPassword = "dev-password" }
        );

        await handler.HandleAsync(CreateCommand("root", TenantPlane.Root), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantDoesNotMatchConfiguredTarget_DoesNothing()
    {
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions
            {
                AdminPassword = "dev-password",
                TenantId = "tenant-a",
            }
        );

        await handler.HandleAsync(
            CreateCommand("tenant-b", TenantPlane.Workload),
            CancellationToken.None
        );
    }

    #endregion
}
