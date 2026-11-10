// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.ResourceServers;

public sealed class ManagementResourceServerSeedHandlerTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private const string TenantId = "tenant-1";
    private const string ManagementIdentifier = "urn:ncode:management";

    private static SeedTenantCommand CreateCommand(IStoreManager storeManager, TenantPlane plane)
    {
        var tenant = new PersistedTenantBuilder
        {
            TenantId = TenantId,
            DisplayName = "Tenant One",
        }.Build();
        return new(
            new TenantSeedContext
            {
                Tenant = tenant,
                Plane = plane,
                StoreManager = storeManager,
            }
        );
    }

    private async ValueTask<PersistedResourceServer?> SeedAsync(
        TenantPlane plane,
        CancellationToken cancellationToken
    )
    {
        var manager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IResourceServerStore>();
        var crypto = MockRepository.Create<ICryptoService>();

        manager.Setup(x => x.GetStore<IResourceServerStore>()).Returns(store.Object).Verifiable();
        store
            .Setup(x =>
                x.GetByIdentifierOrDefaultAsync(ManagementIdentifier, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedResourceServer?)null)
            .Verifiable();
        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
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
        await handler.HandleAsync(CreateCommand(manager.Object, plane), cancellationToken);

        return captured;
    }

    [Fact]
    public async Task HandleAsync_WhenWorkloadPlane_SeedsTenantScopesOnly()
    {
        var captured = await SeedAsync(TenantPlane.Workload, CancellationToken.None);

        Assert.NotNull(captured);
        var values = captured.Scopes.Select(scope => scope.Value).ToList();
        Assert.Contains("read:clients", values);
        Assert.DoesNotContain("read:platform_settings", values);
        Assert.DoesNotContain("read:tenants", values);
        Assert.Contains("read:encryption_keys", values);
        Assert.Contains("update:encryption_keys", values);
        Assert.DoesNotContain("read:platform_encryption_keys", values);
        Assert.DoesNotContain("update:platform_encryption_keys", values);
    }

    [Fact]
    public async Task HandleAsync_WhenRootPlane_SeedsPlatformAndTeamFamilies()
    {
        var captured = await SeedAsync(TenantPlane.Root, CancellationToken.None);

        Assert.NotNull(captured);
        var values = captured.Scopes.Select(scope => scope.Value).ToList();
        Assert.Contains("read:clients", values);
        Assert.Contains("read:platform_settings", values);
        Assert.Contains("create:teams", values);
        Assert.Contains("update:team_settings", values);
        Assert.Contains("create:tenants", values);
        Assert.Contains("read:tenants", values);
        Assert.Contains("read:encryption_keys", values);
        Assert.Contains("update:encryption_keys", values);
        Assert.Contains("read:platform_encryption_keys", values);
        Assert.Contains("update:platform_encryption_keys", values);
    }

    [Fact]
    public async Task HandleAsync_WhenPresent_DoesNothing()
    {
        var manager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IResourceServerStore>();
        var crypto = MockRepository.Create<ICryptoService>();

        var resourceServer = new PersistedResourceServerBuilder
        {
            TenantId = TenantId,
            ResourceServerId = "existing",
            Identifier = ManagementIdentifier,
            Name = "Management",
            IsSystem = true,
            Settings = default,
        }.Build();

        manager.Setup(x => x.GetStore<IResourceServerStore>()).Returns(store.Object).Verifiable();
        store
            .Setup(x =>
                x.GetByIdentifierOrDefaultAsync(ManagementIdentifier, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(resourceServer)
            .Verifiable();

        var handler = new ManagementResourceServerSeedHandler(crypto.Object);

        await handler.HandleAsync(
            CreateCommand(manager.Object, TenantPlane.Workload),
            CancellationToken.None
        );
    }
}
