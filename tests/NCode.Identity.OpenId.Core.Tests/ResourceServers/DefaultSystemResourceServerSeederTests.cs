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
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.ResourceServers;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.ResourceServers;

public sealed class DefaultSystemResourceServerSeederTests
{
    private const string RootTenantId = "root";

    private static IOptions<TenantResolutionOptions> CreateOptions() =>
        Options.Create(new TenantResolutionOptions { RootTenantId = RootTenantId });

    [Fact]
    public async Task SeedAsync_CreatesSystemResourceServerFromProvider()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = mocks.Create<IStoreManagerFactory>();
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IResourceServerStore>();
        var crypto = mocks.Create<ICryptoService>();
        var provider = mocks.Create<ISystemResourceServerProvider>();

        provider
            .Setup(x => x.GetDescriptor())
            .Returns(
                new SystemResourceServerDescriptor
                {
                    Identifier = "urn:test",
                    Name = "Test API",
                    Scopes = [new SystemScopeDescriptor { Value = "read", Description = "Read." }],
                }
            )
            .Verifiable();

        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
            .Returns("generated-rs-id")
            .Verifiable();

        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object)
            .Verifiable();
        manager.Setup(x => x.GetStore<IResourceServerStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        PersistedResourceServer? captured = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedResourceServer>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedResourceServer resourceServer, CancellationToken _) =>
                    captured = resourceServer
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var seeder = new DefaultSystemResourceServerSeeder(
            factory.Object,
            crypto.Object,
            CreateOptions(),
            [provider.Object]
        );

        await seeder.SeedAsync("tenant-1", CancellationToken.None);

        mocks.Verify();
        Assert.NotNull(captured);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("generated-rs-id", captured.ResourceServerId);
        Assert.Equal("urn:test", captured.Identifier);
        Assert.True(captured.IsSystem);
        var scope = Assert.Single(captured.Scopes);
        Assert.Equal("read", scope.Value);
        Assert.True(scope.IsSystem);
    }

    [Fact]
    public async Task SeedAsync_WhenNoProviders_DoesNothing()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = mocks.Create<IStoreManagerFactory>();
        var crypto = mocks.Create<ICryptoService>();

        // No store scaffold: a strict-mock failure would mean the seeder opened a unit of work with no providers.
        var seeder = new DefaultSystemResourceServerSeeder(
            factory.Object,
            crypto.Object,
            CreateOptions(),
            []
        );

        await seeder.SeedAsync("tenant-1", CancellationToken.None);
    }

    [Fact]
    public async Task SeedAsync_WhenControlPlaneProvider_AndNotRootTenant_SkipsSeeding()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = mocks.Create<IStoreManagerFactory>();
        var crypto = mocks.Create<ICryptoService>();
        var provider = mocks.Create<ISystemResourceServerProvider>();

        provider
            .Setup(x => x.GetDescriptor())
            .Returns(
                new SystemResourceServerDescriptor
                {
                    Identifier = "urn:control",
                    Name = "Control API",
                    Plane = SystemResourceServerPlane.Control,
                    Scopes =
                    [
                        new SystemScopeDescriptor
                        {
                            Value = "read:tenants",
                            Description = "Read tenants.",
                        },
                    ],
                }
            )
            .Verifiable();

        // No store scaffold: a strict-mock failure would mean a control-plane RS leaked into a workload tenant.
        var seeder = new DefaultSystemResourceServerSeeder(
            factory.Object,
            crypto.Object,
            CreateOptions(),
            [provider.Object]
        );

        await seeder.SeedAsync("workload-tenant", CancellationToken.None);

        mocks.Verify();
    }

    [Fact]
    public async Task SeedAsync_WhenControlPlaneProvider_AndRootTenant_Seeds()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = mocks.Create<IStoreManagerFactory>();
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IResourceServerStore>();
        var crypto = mocks.Create<ICryptoService>();
        var provider = mocks.Create<ISystemResourceServerProvider>();

        provider
            .Setup(x => x.GetDescriptor())
            .Returns(
                new SystemResourceServerDescriptor
                {
                    Identifier = "urn:control",
                    Name = "Control API",
                    Plane = SystemResourceServerPlane.Control,
                    Scopes =
                    [
                        new SystemScopeDescriptor
                        {
                            Value = "read:tenants",
                            Description = "Read tenants.",
                        },
                    ],
                }
            )
            .Verifiable();

        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
            .Returns("generated-rs-id")
            .Verifiable();

        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object)
            .Verifiable();
        manager.Setup(x => x.GetStore<IResourceServerStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        PersistedResourceServer? captured = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedResourceServer>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedResourceServer resourceServer, CancellationToken _) =>
                    captured = resourceServer
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var seeder = new DefaultSystemResourceServerSeeder(
            factory.Object,
            crypto.Object,
            CreateOptions(),
            [provider.Object]
        );

        await seeder.SeedAsync(RootTenantId, CancellationToken.None);

        mocks.Verify();
        Assert.NotNull(captured);
        Assert.Equal(RootTenantId, captured.TenantId);
        Assert.Equal("urn:control", captured.Identifier);
    }

    [Fact]
    public async Task SeedAsync_WhenProvidersShareIdentifier_MergesIntoOneResourceServer()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = mocks.Create<IStoreManagerFactory>();
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IResourceServerStore>();
        var crypto = mocks.Create<ICryptoService>();
        var first = mocks.Create<ISystemResourceServerProvider>();
        var second = mocks.Create<ISystemResourceServerProvider>();

        first
            .Setup(x => x.GetDescriptor())
            .Returns(
                new SystemResourceServerDescriptor
                {
                    Identifier = "urn:test",
                    Name = "Test API",
                    Scopes =
                    [
                        new SystemScopeDescriptor { Value = "read:a", Description = "Read A." },
                        new SystemScopeDescriptor { Value = "shared", Description = "Shared." },
                    ],
                }
            )
            .Verifiable();
        second
            .Setup(x => x.GetDescriptor())
            .Returns(
                new SystemResourceServerDescriptor
                {
                    Identifier = "urn:test",
                    Name = "Test API",
                    Scopes =
                    [
                        new SystemScopeDescriptor { Value = "read:b", Description = "Read B." },
                        new SystemScopeDescriptor { Value = "shared", Description = "Shared." },
                    ],
                }
            )
            .Verifiable();

        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
            .Returns("generated-rs-id")
            .Verifiable();

        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object)
            .Verifiable();
        manager.Setup(x => x.GetStore<IResourceServerStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        var added = new List<PersistedResourceServer>();
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedResourceServer>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedResourceServer resourceServer, CancellationToken _) =>
                    added.Add(resourceServer)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var seeder = new DefaultSystemResourceServerSeeder(
            factory.Object,
            crypto.Object,
            CreateOptions(),
            [first.Object, second.Object]
        );

        await seeder.SeedAsync("tenant-1", CancellationToken.None);

        mocks.Verify();
        var captured = Assert.Single(added);
        Assert.Equal("urn:test", captured.Identifier);
        Assert.Equal(
            new[] { "read:a", "shared", "read:b" }.OrderBy(value => value),
            captured.Scopes.Select(scope => scope.Value).OrderBy(value => value)
        );
    }
}
