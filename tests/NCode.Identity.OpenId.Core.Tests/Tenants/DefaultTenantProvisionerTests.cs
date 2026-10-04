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

using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DefaultTenantProvisionerTests
{
    private const string RootTenantId = "root";

    private static DefaultTenantProvisioner CreateProvisioner(
        IStoreManagerFactory factory,
        IAmbientTenantAccessor ambient,
        IMediator mediator
    ) =>
        new(
            factory,
            ambient,
            Options.Create(new TenantResolutionOptions { RootTenantId = RootTenantId }),
            mediator
        );

    private static PersistedTenant CreateTenant(string tenantId) =>
        new()
        {
            TenantId = tenantId,
            DomainName = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = null!,
            Secrets = null!,
        };

    [Fact]
    public async Task ProvisionAsync_WhenRootTenantIsNew_AddsRowSeedsAndSaves()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = mocks.Create<IStoreManagerFactory>();
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<ITenantStore>();
        var ambient = mocks.Create<IAmbientTenantAccessor>();
        var scope = mocks.Create<IDisposable>();
        var mediator = mocks.Create<IMediator>();

        ambient.Setup(x => x.BeginScope(RootTenantId)).Returns(scope.Object).Verifiable();
        scope.Setup(x => x.Dispose()).Verifiable();
        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object)
            .Verifiable();
        manager.Setup(x => x.GetStore<ITenantStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        store
            .Setup(x => x.GetOrDefaultAsync(RootTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        store
            .Setup(x => x.AddAsync(It.IsAny<PersistedTenant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SeedTenantCommand captured = default;
        mediator
            .Setup(x => x.SendAsync(It.IsAny<SeedTenantCommand>(), It.IsAny<CancellationToken>()))
            .Callback((SeedTenantCommand command, CancellationToken _) => captured = command)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var provisioner = CreateProvisioner(factory.Object, ambient.Object, mediator.Object);

        var result = await provisioner.ProvisionAsync(
            RootTenantId,
            "Root Tenant",
            CancellationToken.None
        );

        mocks.Verify();
        Assert.Equal(RootTenantId, result.TenantId);
        Assert.Equal(TenantPlane.Root, captured.Context.Plane);
        Assert.True(captured.Context.IsNewlyProvisioned);
    }

    [Fact]
    public async Task ProvisionAsync_WhenTenantAlreadyExists_DoesNotAddRow()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = mocks.Create<IStoreManagerFactory>();
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<ITenantStore>();
        var ambient = mocks.Create<IAmbientTenantAccessor>();
        var scope = mocks.Create<IDisposable>();
        var mediator = mocks.Create<IMediator>();

        var existing = CreateTenant(RootTenantId);

        ambient.Setup(x => x.BeginScope(RootTenantId)).Returns(scope.Object).Verifiable();
        scope.Setup(x => x.Dispose()).Verifiable();
        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object)
            .Verifiable();
        manager.Setup(x => x.GetStore<ITenantStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        store
            .Setup(x => x.GetOrDefaultAsync(RootTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .Verifiable();

        SeedTenantCommand captured = default;
        mediator
            .Setup(x => x.SendAsync(It.IsAny<SeedTenantCommand>(), It.IsAny<CancellationToken>()))
            .Callback((SeedTenantCommand command, CancellationToken _) => captured = command)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var provisioner = CreateProvisioner(factory.Object, ambient.Object, mediator.Object);

        var result = await provisioner.ProvisionAsync(
            RootTenantId,
            "Root Tenant",
            CancellationToken.None
        );

        mocks.Verify();
        Assert.Same(existing, result);
        Assert.False(captured.Context.IsNewlyProvisioned);
    }

    [Fact]
    public async Task ProvisionAsync_WhenWorkloadTenant_ProvisionsRootFirst()
    {
        const string workloadTenantId = "default";

        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = mocks.Create<IStoreManagerFactory>();
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<ITenantStore>();
        var ambient = mocks.Create<IAmbientTenantAccessor>();
        var scope = mocks.Create<IDisposable>();
        var mediator = mocks.Create<IMediator>();

        ambient.Setup(x => x.BeginScope(It.IsAny<string>())).Returns(scope.Object);
        scope.Setup(x => x.Dispose());
        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object);
        manager.Setup(x => x.GetStore<ITenantStore>()).Returns(store.Object);
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        store
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null);
        store
            .Setup(x => x.AddAsync(It.IsAny<PersistedTenant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        mediator
            .Setup(x => x.SendAsync(It.IsAny<SeedTenantCommand>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var provisioner = CreateProvisioner(factory.Object, ambient.Object, mediator.Object);

        var result = await provisioner.ProvisionAsync(
            workloadTenantId,
            "Default",
            CancellationToken.None
        );

        Assert.Equal(workloadTenantId, result.TenantId);
        ambient.Verify(x => x.BeginScope(RootTenantId), Times.Once);
        ambient.Verify(x => x.BeginScope(workloadTenantId), Times.Once);
        store.Verify(
            x => x.GetOrDefaultAsync(RootTenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
