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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Extensions.DataProtection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class BootstrapAdminSeedHandlerTests : BaseTests
{
    private const string ClientId = "bootstrap-admin";
    private const string ClientSecret = "secret-value";
    private const string WorkloadTenantId = "default";

    private BootstrapAdminSeedHandler CreateHandler(BootstrapAdminOptions bootstrapOptions)
    {
        // Strict mocks: none of these collaborators are touched on the no-op guard paths.
        var mockDataProtectorFactory = CreateStrictMock<IDataProtectorFactory<PersistedSecret>>();
        var mockLogger = CreateLooseMock<ILogger<BootstrapAdminSeedHandler>>();

        return new BootstrapAdminSeedHandler(
            Options.Create(bootstrapOptions),
            mockDataProtectorFactory.Object,
            TimeProvider.System,
            mockLogger.Object
        );
    }

    private SeedTenantCommand CreateCommand(string tenantId, TenantPlane plane)
    {
        // Strict mock: the store manager is untouched on the guard paths under test.
        var mockStoreManager = CreateStrictMock<IStoreManager>();

        var persistedTenant = new PersistedTenant
        {
            TenantId = tenantId,
            DomainName = null,
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
    public async Task HandleAsync_WhenNotConfigured_DoesNothing()
    {
        var handler = CreateHandler(new BootstrapAdminOptions());

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRootPlane_DoesNothing()
    {
        var handler = CreateHandler(
            new BootstrapAdminOptions { ClientId = ClientId, ClientSecret = ClientSecret }
        );

        await handler.HandleAsync(CreateCommand("root", TenantPlane.Root), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantDoesNotMatchConfiguredTarget_DoesNothing()
    {
        var handler = CreateHandler(
            new BootstrapAdminOptions
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
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
