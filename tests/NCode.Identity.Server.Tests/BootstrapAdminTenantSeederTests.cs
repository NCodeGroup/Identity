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
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class BootstrapAdminTenantSeederTests : BaseTests
{
    private const string ClientId = "bootstrap-admin";
    private const string ClientSecret = "secret-value";
    private const string RootTenantId = "root";
    private const string WorkloadTenantId = "default";

    private BootstrapAdminTenantSeeder CreateSeeder(BootstrapAdminOptions bootstrapOptions)
    {
        // Strict mocks: none of these collaborators are touched on the no-op guard paths.
        var mockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        var mockAmbientTenantAccessor = CreateStrictMock<IAmbientTenantAccessor>();
        var mockDataProtectorFactory = CreateStrictMock<IDataProtectorFactory<PersistedSecret>>();
        var mockLogger = CreateLooseMock<ILogger<BootstrapAdminTenantSeeder>>();

        return new BootstrapAdminTenantSeeder(
            Options.Create(bootstrapOptions),
            Options.Create(new TenantResolutionOptions { RootTenantId = RootTenantId }),
            mockStoreManagerFactory.Object,
            mockAmbientTenantAccessor.Object,
            mockDataProtectorFactory.Object,
            TimeProvider.System,
            mockLogger.Object
        );
    }

    #region SeedAsync Tests

    [Fact]
    public async Task SeedAsync_WhenNotConfigured_DoesNothing()
    {
        var seeder = CreateSeeder(new BootstrapAdminOptions());

        await seeder.SeedAsync(WorkloadTenantId, CancellationToken.None);
    }

    [Fact]
    public async Task SeedAsync_WhenTargetingRootTenant_DoesNothing()
    {
        var seeder = CreateSeeder(
            new BootstrapAdminOptions { ClientId = ClientId, ClientSecret = ClientSecret }
        );

        await seeder.SeedAsync(RootTenantId, CancellationToken.None);
    }

    [Fact]
    public async Task SeedAsync_WhenTenantDoesNotMatchConfiguredTarget_DoesNothing()
    {
        var seeder = CreateSeeder(
            new BootstrapAdminOptions
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                TenantId = "tenant-a",
            }
        );

        await seeder.SeedAsync("tenant-b", CancellationToken.None);
    }

    #endregion
}
