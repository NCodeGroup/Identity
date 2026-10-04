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

using Moq;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Management.ResourceServers;

public sealed class ManagementResourceServerSeedHandlerTests
{
    private const string TenantId = "tenant-1";
    private const string ManagementIdentifier = "urn:ncode:management";

    private static SeedTenantCommand CreateCommand(IStoreManager storeManager, TenantPlane plane) =>
        new(
            new TenantSeedContext
            {
                Tenant = new PersistedTenant
                {
                    TenantId = TenantId,
                    DomainName = null,
                    ConcurrencyToken = "token",
                    IsDisabled = false,
                    DisplayName = "Tenant One",
                    Settings = null!,
                    Secrets = null!,
                },
                Plane = plane,
                IsNewlyProvisioned = true,
                StoreManager = storeManager,
            }
        );

    private static PersistedResourceServer? Seed(TenantPlane plane)
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IResourceServerStore>();
        var crypto = mocks.Create<ICryptoService>();

        manager.Setup(x => x.GetStore<IResourceServerStore>()).Returns(store.Object).Verifiable();
        store
            .Setup(x =>
                x.GetByIdentifierOrDefaultAsync(ManagementIdentifier, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedResourceServer?)null)
            .Verifiable();
        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
            .Returns("generated-rs-id")
            .Verifiable();

        PersistedResourceServer? captured = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedResourceServer>(), It.IsAny<CancellationToken>())
            )
            .Callback((PersistedResourceServer rs, CancellationToken _) => captured = rs)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var handler = new ManagementResourceServerSeedHandler(crypto.Object);
        handler
            .HandleAsync(CreateCommand(manager.Object, plane), CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        mocks.Verify();
        return captured;
    }

    [Fact]
    public void HandleAsync_WhenWorkloadPlane_SeedsTenantScopesOnly()
    {
        var captured = Seed(TenantPlane.Workload);

        Assert.NotNull(captured);
        var values = captured.Scopes.Select(scope => scope.Value).ToList();
        Assert.Contains("read:clients", values);
        Assert.DoesNotContain("read:servers", values);
        Assert.DoesNotContain("read:tenants", values);
    }

    [Fact]
    public void HandleAsync_WhenRootPlane_SeedsTenantAndControlScopes()
    {
        var captured = Seed(TenantPlane.Root);

        Assert.NotNull(captured);
        var values = captured.Scopes.Select(scope => scope.Value).ToList();
        Assert.Contains("read:clients", values);
        Assert.Contains("read:servers", values);
        Assert.Contains("read:tenants", values);
    }

    [Fact]
    public async Task HandleAsync_WhenPresent_DoesNothing()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IResourceServerStore>();
        var crypto = mocks.Create<ICryptoService>();

        manager.Setup(x => x.GetStore<IResourceServerStore>()).Returns(store.Object).Verifiable();
        store
            .Setup(x =>
                x.GetByIdentifierOrDefaultAsync(ManagementIdentifier, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new PersistedResourceServer
                {
                    TenantId = TenantId,
                    ResourceServerId = "existing",
                    Identifier = ManagementIdentifier,
                    ConcurrencyToken = "token",
                    Name = "Management",
                    IsSystem = true,
                    IsDisabled = false,
                    Settings = default,
                    Scopes = [],
                }
            )
            .Verifiable();

        var handler = new ManagementResourceServerSeedHandler(crypto.Object);

        await handler.HandleAsync(
            CreateCommand(manager.Object, TenantPlane.Workload),
            CancellationToken.None
        );

        mocks.Verify();
    }
}
