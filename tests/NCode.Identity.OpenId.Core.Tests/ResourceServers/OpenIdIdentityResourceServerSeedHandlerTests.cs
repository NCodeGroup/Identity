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
using NCode.Identity.OpenId.Core.ResourceServers;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.ResourceServers;

public sealed class OpenIdIdentityResourceServerSeedHandlerTests
{
    private const string TenantId = "tenant-1";
    private const string OpenIdIdentifier = "urn:ncode:openid";

    private static SeedTenantCommand CreateCommand(IStoreManager storeManager) =>
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
                Plane = TenantPlane.Workload,
                IsNewlyProvisioned = true,
                StoreManager = storeManager,
            }
        );

    [Fact]
    public async Task HandleAsync_WhenAbsent_AddsResourceServer()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IResourceServerStore>();
        var crypto = mocks.Create<ICryptoService>();

        manager.Setup(x => x.GetStore<IResourceServerStore>()).Returns(store.Object).Verifiable();
        store
            .Setup(x =>
                x.GetByIdentifierOrDefaultAsync(OpenIdIdentifier, It.IsAny<CancellationToken>())
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

        var handler = new OpenIdIdentityResourceServerSeedHandler(crypto.Object);

        await handler.HandleAsync(CreateCommand(manager.Object), CancellationToken.None);

        mocks.Verify();
        Assert.NotNull(captured);
        Assert.Equal(TenantId, captured.TenantId);
        Assert.Equal(OpenIdIdentifier, captured.Identifier);
        Assert.True(captured.IsSystem);
        Assert.Contains(captured.Scopes, scope => scope.Value == "openid");
        Assert.All(captured.Scopes, scope => Assert.True(scope.IsSystem));
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
                x.GetByIdentifierOrDefaultAsync(OpenIdIdentifier, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new PersistedResourceServer
                {
                    TenantId = TenantId,
                    ResourceServerId = "existing",
                    Identifier = OpenIdIdentifier,
                    ConcurrencyToken = "token",
                    Name = "OpenID Connect",
                    IsSystem = true,
                    IsDisabled = false,
                    Settings = default,
                    Scopes = [],
                }
            )
            .Verifiable();

        var handler = new OpenIdIdentityResourceServerSeedHandler(crypto.Object);

        await handler.HandleAsync(CreateCommand(manager.Object), CancellationToken.None);

        mocks.Verify();
    }
}
