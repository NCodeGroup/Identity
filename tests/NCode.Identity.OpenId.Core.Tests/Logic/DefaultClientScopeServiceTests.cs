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
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Logic;

public sealed class DefaultClientScopeServiceTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IResourceServerStore> MockResourceServerStore { get; }
    private Mock<IClientGrantStore> MockClientGrantStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private DefaultClientScopeService Service { get; }

    public DefaultClientScopeServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockResourceServerStore = MockRepository.Create<IResourceServerStore>();
        MockClientGrantStore = MockRepository.Create<IClientGrantStore>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();

        Service = new DefaultClientScopeService(
            MockStoreManagerFactory.Object,
            MockAmbientTenantAccessor.Object
        );
    }

    public void Dispose() => MockRepository.Verify();

    #region Helpers

    private static ResourceServerScopeProjection Projection(
        string resourceServerId,
        bool isSystem,
        params string[] scopes
    ) =>
        new()
        {
            Identifier = $"urn:{resourceServerId}",
            ResourceServerId = resourceServerId,
            IsSystem = isSystem,
            ScopeValues = scopes,
        };

    private static PersistedClientGrant Grant(string resourceServerId, params string[] scopes) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ResourceServerId = resourceServerId,
            ConcurrencyToken = "ct",
            Scopes = scopes,
        };

    private void SetupStores()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IResourceServerStore>())
            .Returns(MockResourceServerStore.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IClientGrantStore>())
            .Returns(MockClientGrantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupResourceServerStoreOnly()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IResourceServerStore>())
            .Returns(MockResourceServerStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupScopeProjectionsBySystemOrIds(
        params ResourceServerScopeProjection[] projections
    )
    {
        MockResourceServerStore
            .Setup(x =>
                x.GetEnabledScopeProjectionsBySystemOrIdsAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(projections)
            .Verifiable();
    }

    private void SetupScopeProjectionsByScopes(params ResourceServerScopeProjection[] projections)
    {
        MockResourceServerStore
            .Setup(x =>
                x.GetEnabledScopeProjectionsByScopesAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(projections)
            .Verifiable();
    }

    private void SetupEnabledIdentifiers(params string[] identifiers)
    {
        MockResourceServerStore
            .Setup(x => x.GetEnabledIdentifiersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(identifiers)
            .Verifiable();
    }

    private void SetupClientGrants(params PersistedClientGrant[] grants)
    {
        MockClientGrantStore
            .Setup(x =>
                x.GetPageAsync(ClientId, null, It.IsAny<int>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new PagedResult<PersistedClientGrant> { Items = grants, NextCursor = null }
            )
            .Verifiable();
    }

    #endregion

    [Fact]
    public async Task GetAllowedScopesAsync_IncludesSystemScopesImplicitly()
    {
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(Mock.Of<IDisposable>())
            .Verifiable();
        SetupStores();
        SetupScopeProjectionsBySystemOrIds(
            Projection("rs-openid", isSystem: true, "openid", "offline_access")
        );
        SetupClientGrants();

        var allowed = await Service.GetAllowedScopesAsync(
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.Contains("openid", allowed);
        Assert.Contains("offline_access", allowed);
    }

    [Fact]
    public async Task GetAllowedScopesAsync_NonSystemRequiresGrantAndIntersects()
    {
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(Mock.Of<IDisposable>())
            .Verifiable();
        SetupStores();
        SetupScopeProjectionsBySystemOrIds(
            Projection("rs-api", isSystem: false, "read:x", "write:x")
        );
        // Granted read:x (kept) and stale:y (ignored, not defined on the resource server).
        SetupClientGrants(Grant("rs-api", "read:x", "stale:y"));

        var allowed = await Service.GetAllowedScopesAsync(
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.Contains("read:x", allowed);
        Assert.DoesNotContain("write:x", allowed); // defined on the RS but not granted
        Assert.DoesNotContain("stale:y", allowed); // granted but not defined on the RS
    }

    [Fact]
    public async Task GetAllowedScopesAsync_NonSystemWithoutGrant_IsExcluded()
    {
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(Mock.Of<IDisposable>())
            .Verifiable();
        SetupStores();
        // A non-system resource server with no grant is filtered out by the store (not system, not in the
        // client's granted ids), so none are returned.
        SetupScopeProjectionsBySystemOrIds();
        SetupClientGrants();

        var allowed = await Service.GetAllowedScopesAsync(
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.Empty(allowed);
    }

    [Fact]
    public async Task GetAllowedScopesAsync_DisabledResourceServer_IsExcluded()
    {
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(Mock.Of<IDisposable>())
            .Verifiable();
        SetupStores();
        // The store excludes disabled resource servers, so none are returned.
        SetupScopeProjectionsBySystemOrIds();
        SetupClientGrants();

        var allowed = await Service.GetAllowedScopesAsync(
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.DoesNotContain("dead:z", allowed);
    }

    [Fact]
    public async Task ResolveAudiencesAsync_ReturnsIdentifiersOfResourceServersOwningTheScopes()
    {
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(Mock.Of<IDisposable>())
            .Verifiable();
        SetupResourceServerStoreOnly();
        // The store returns only enabled resource servers owning one of the scopes (the disabled 'rs-dead' is
        // excluded even though it also owns 'read:x').
        SetupScopeProjectionsByScopes(
            Projection("rs-api", isSystem: false, "read:x", "write:x"),
            Projection("rs-openid", isSystem: true, "openid")
        );

        var audiences = await Service.ResolveAudiencesAsync(
            TenantId,
            ["read:x", "openid"],
            CancellationToken.None
        );

        Assert.Equal(new[] { "urn:rs-api", "urn:rs-openid" }, audiences);
    }

    [Fact]
    public async Task ResolveAudiencesAsync_WhenNoScopes_ReturnsEmpty()
    {
        var audiences = await Service.ResolveAudiencesAsync(TenantId, [], CancellationToken.None);

        Assert.Empty(audiences);
    }

    [Fact]
    public async Task GetEnabledResourceServerIdentifiersAsync_ExcludesDisabled()
    {
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(Mock.Of<IDisposable>())
            .Verifiable();
        SetupResourceServerStoreOnly();
        // The store returns only the enabled identifiers (the disabled 'rs-dead' is excluded).
        SetupEnabledIdentifiers("urn:rs-api", "urn:rs-openid");

        var identifiers = await Service.GetEnabledResourceServerIdentifiersAsync(
            TenantId,
            CancellationToken.None
        );

        Assert.Equal(["urn:rs-api", "urn:rs-openid"], identifiers.Order());
    }
}
