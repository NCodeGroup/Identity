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

using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ResourceServers;

public sealed class DefaultResourceServerCredentialResolverTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IResourceServerStore> MockResourceServerStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<ISecretSerializer> MockSecretSerializer { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private Mock<ISecretKeyCollectionProviderFactory> MockSecretKeyCollectionProviderFactory { get; }
    private ISecretProtector Protector { get; }

    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdTenant> MockTenant { get; }
    private Mock<IReadOnlySettingCollectionProvider> MockSettingsProvider { get; }
    private Mock<IReadOnlySettingCollection> MockTenantSettings { get; }

    private DefaultResourceServerCredentialResolver Resolver { get; }

    public DefaultResourceServerCredentialResolverTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockResourceServerStore = MockRepository.Create<IResourceServerStore>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockSecretSerializer = MockRepository.Create<ISecretSerializer>();
        MockSecretProtectorProvider = MockRepository.Create<ISecretProtectorProvider>();
        MockSecretKeyCollectionProviderFactory =
            MockRepository.Create<ISecretKeyCollectionProviderFactory>();
        Protector = MockRepository.Create<ISecretProtector>().Object;

        MockContext = MockRepository.Create<OpenIdContext>();
        MockTenant = MockRepository.Create<OpenIdTenant>();
        MockSettingsProvider = MockRepository.Create<IReadOnlySettingCollectionProvider>();
        MockTenantSettings = MockRepository.Create<IReadOnlySettingCollection>();

        Resolver = new DefaultResourceServerCredentialResolver(
            MockStoreManagerFactory.Object,
            MockAmbientTenantAccessor.Object,
            MockEffectiveSettingsResolver.Object,
            MockSecretSerializer.Object,
            MockSecretProtectorProvider.Object,
            MockSecretKeyCollectionProviderFactory.Object
        );
    }

    public void Dispose() => MockRepository.Verify();

    #region Helpers

    private static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>());

    private static PersistedScope Scope(string value) =>
        new()
        {
            Value = value,
            Description = null,
            IsSystem = false,
        };

    private static PersistedResourceServer ResourceServer(
        string resourceServerId,
        params string[] scopes
    ) =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = resourceServerId,
            Identifier = $"urn:{resourceServerId}",
            ConcurrencyToken = "ct",
            Name = resourceServerId,
            IsSystem = false,
            IsDisabled = false,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = TenantId,
                ResourceServerId = resourceServerId,
                Value = EmptyObject(),
            },
            Scopes = scopes.Select(Scope).ToList(),
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = TenantId,
                ResourceServerId = resourceServerId,
                Value = [],
            },
        };

    private void SetupContext()
    {
        MockContext.SetupGet(x => x.Tenant).Returns(MockTenant.Object).Verifiable();
        MockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(Mock.Of<IDisposable>())
            .Verifiable();
    }

    // A matched resource server's effective settings are read from the tenant's, which only a match reads, and its
    // secrets are read through the tenant's protector (ADR-0235).
    private void SetupMaterialization()
    {
        MockSecretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant(TenantId),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Protector)
            .Verifiable();
        MockTenant
            .SetupGet(x => x.SettingsProvider)
            .Returns(MockSettingsProvider.Object)
            .Verifiable();
        MockSettingsProvider
            .SetupGet(x => x.Collection)
            .Returns(MockTenantSettings.Object)
            .Verifiable();
    }

    private void SetupStore()
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

    // The store applies the enabled/scope/identifier filtering; these tests supply the already-filtered matches.
    private void SetupFindEnabledByScopes(params PersistedResourceServer[] resourceServers)
    {
        MockResourceServerStore
            .Setup(x =>
                x.FindEnabledByScopesAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<IReadOnlyCollection<string>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(resourceServers)
            .Verifiable();
    }

    // The resolver's merge of the tenant-effective settings with each resource server's document yields the effective
    // settings carried on each resolved resource server; the tests do not read those values.
    private void SetupSettingsMerge()
    {
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    MockTenantSettings.Object,
                    OpenIdSettingScopes.ResourceServer,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(MockRepository.Create<ISettingCollection>().Object)
            .Verifiable();
    }

    // Every match's own keys are materialized into a provider the resolution owns, so each provider must be disposed.
    private void SetupSecretsProviders()
    {
        MockSecretSerializer
            .Setup(x =>
                x.DeserializeSecrets(Protector, It.IsAny<IReadOnlyCollection<PersistedSecret>>())
            )
            .Returns(Array.Empty<SecretKey>())
            .Verifiable();
        MockSecretKeyCollectionProviderFactory
            .Setup(x => x.CreateStatic(It.IsAny<IEnumerable<SecretKey>>()))
            .Returns(() => CreateDisposableSecretsProvider().Object)
            .Verifiable();
    }

    private Mock<ISecretKeyCollectionProvider> CreateDisposableSecretsProvider()
    {
        var mockProvider = MockRepository.Create<ISecretKeyCollectionProvider>();
        mockProvider.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        return mockProvider;
    }

    #endregion

    [Fact]
    public async Task ResolveAsync_NoScopes_ReturnsEmptyResolution()
    {
        await using var resolution = await Resolver.ResolveAsync(
            MockContext.Object,
            [],
            [],
            CancellationToken.None
        );

        Assert.Empty(resolution.ResourceServers);
        Assert.Null(resolution.Single);
        Assert.Empty(resolution.Audiences);
    }

    [Fact]
    public async Task ResolveAsync_NoMatchingResourceServer_ReturnsEmptyResolution()
    {
        SetupContext();
        SetupStore();
        // The store returns no match for a scope no enabled resource server owns.
        SetupFindEnabledByScopes();

        await using var resolution = await Resolver.ResolveAsync(
            MockContext.Object,
            ["read:x"],
            [],
            CancellationToken.None
        );

        Assert.Empty(resolution.ResourceServers);
        Assert.Null(resolution.Single);
    }

    [Fact]
    public async Task ResolveAsync_DisabledResourceServer_IsIgnored()
    {
        SetupContext();
        SetupStore();
        // The store excludes disabled resource servers, so no match is returned.
        SetupFindEnabledByScopes();

        await using var resolution = await Resolver.ResolveAsync(
            MockContext.Object,
            ["read:x"],
            [],
            CancellationToken.None
        );

        Assert.Empty(resolution.ResourceServers);
    }

    [Fact]
    public async Task ResolveAsync_SingleMatch_SetsSingleWithItsSecretsProvider()
    {
        SetupContext();
        SetupStore();
        SetupMaterialization();
        SetupSettingsMerge();
        SetupFindEnabledByScopes(ResourceServer("rs-api", "read:x"));

        var mockProvider = CreateDisposableSecretsProvider();
        MockSecretSerializer
            .Setup(x =>
                x.DeserializeSecrets(Protector, It.IsAny<IReadOnlyCollection<PersistedSecret>>())
            )
            .Returns(Array.Empty<SecretKey>())
            .Verifiable();
        MockSecretKeyCollectionProviderFactory
            .Setup(x => x.CreateStatic(It.IsAny<IEnumerable<SecretKey>>()))
            .Returns(mockProvider.Object)
            .Verifiable();

        await using var resolution = await Resolver.ResolveAsync(
            MockContext.Object,
            ["read:x"],
            [],
            CancellationToken.None
        );

        Assert.Single(resolution.ResourceServers);
        var single = resolution.Single;
        Assert.NotNull(single);
        Assert.Equal("rs-api", single.ResourceServerId);
        Assert.Same(mockProvider.Object, single.SecretsProvider);
        Assert.Equal(["urn:rs-api"], resolution.Audiences);
    }

    [Fact]
    public async Task ResolveAsync_MultipleMatches_SingleNullAndAudiencesSorted()
    {
        SetupContext();
        SetupStore();
        SetupMaterialization();
        SetupSettingsMerge();
        SetupSecretsProviders();
        SetupFindEnabledByScopes(
            ResourceServer("rs-b", "read:x"),
            ResourceServer("rs-a", "read:x")
        );

        await using var resolution = await Resolver.ResolveAsync(
            MockContext.Object,
            ["read:x"],
            [],
            CancellationToken.None
        );

        Assert.Equal(2, resolution.ResourceServers.Count);
        Assert.Null(resolution.Single);
        // Audiences are ordered by identifier (urn:rs-a before urn:rs-b).
        Assert.Equal(["urn:rs-a", "urn:rs-b"], resolution.Audiences);
    }

    [Fact]
    public async Task ResolveAsync_MultipleMatches_EachCarriesItsOwnSecretsProvider()
    {
        SetupContext();
        SetupStore();
        SetupMaterialization();
        SetupSettingsMerge();
        SetupSecretsProviders();
        SetupFindEnabledByScopes(
            ResourceServer("rs-a", "read:x"),
            ResourceServer("rs-b", "read:x")
        );

        await using var resolution = await Resolver.ResolveAsync(
            MockContext.Object,
            ["read:x"],
            [],
            CancellationToken.None
        );

        // Each provider is disposed with the resolution, which the strict DisposeAsync setups verify.
        Assert.NotSame(
            resolution.ResourceServers[0].SecretsProvider,
            resolution.ResourceServers[1].SecretsProvider
        );
    }

    [Fact]
    public async Task ResolveAsync_WhenALaterSecretsProviderCannotBeBuilt_DisposesTheEarlierOnes()
    {
        SetupContext();
        SetupStore();
        SetupMaterialization();
        SetupSettingsMerge();
        SetupFindEnabledByScopes(
            ResourceServer("rs-a", "read:x"),
            ResourceServer("rs-b", "read:x")
        );

        var mockFirstProvider = CreateDisposableSecretsProvider();
        var created = 0;
        MockSecretSerializer
            .Setup(x =>
                x.DeserializeSecrets(Protector, It.IsAny<IReadOnlyCollection<PersistedSecret>>())
            )
            .Returns(Array.Empty<SecretKey>())
            .Verifiable();
        MockSecretKeyCollectionProviderFactory
            .Setup(x => x.CreateStatic(It.IsAny<IEnumerable<SecretKey>>()))
            .Returns(() =>
                ++created == 1
                    ? mockFirstProvider.Object
                    : throw new InvalidOperationException("The stored key cannot be read.")
            )
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Resolver.ResolveAsync(MockContext.Object, ["read:x"], [], CancellationToken.None)
        );
    }

    [Fact]
    public async Task ResolveAsync_RequestedResourceNarrowsAudience()
    {
        SetupContext();
        SetupStore();
        SetupMaterialization();
        SetupSettingsMerge();
        SetupSecretsProviders();
        // The store applies the RFC 8707 identifier narrowing, so rs-c is not returned.
        SetupFindEnabledByScopes(
            ResourceServer("rs-a", "read:x"),
            ResourceServer("rs-b", "read:x")
        );

        await using var resolution = await Resolver.ResolveAsync(
            MockContext.Object,
            ["read:x"],
            ["urn:rs-a", "urn:rs-b"],
            CancellationToken.None
        );

        // Only the requested resources are targeted, even though rs-c also owns the scope (RFC 8707).
        Assert.Equal(["urn:rs-a", "urn:rs-b"], resolution.Audiences);
    }

    [Fact]
    public async Task ResolveAsync_RequestedResourceNotScopeOwning_ThrowsInvalidTarget()
    {
        SetupContext();
        SetupStore();
        SetupErrorFactory();
        // The requested 'urn:unknown' narrows to no enabled, scope-owning resource server.
        SetupFindEnabledByScopes();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Resolver.ResolveAsync(
                MockContext.Object,
                ["read:x"],
                ["urn:unknown"],
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ResolveAsync_RequestedResourceWithoutScopes_ThrowsInvalidTarget()
    {
        SetupErrorFactory();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Resolver.ResolveAsync(
                MockContext.Object,
                [],
                ["urn:rs-a"],
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ResolveByIdAsync_WhenTheResourceServerIsEnabled_ResolvesItWithItsScopes()
    {
        SetupContext();
        SetupStore();
        SetupMaterialization();
        SetupSettingsMerge();
        var mockProvider = CreateDisposableSecretsProvider();
        MockSecretSerializer
            .Setup(x =>
                x.DeserializeSecrets(Protector, It.IsAny<IReadOnlyCollection<PersistedSecret>>())
            )
            .Returns(Array.Empty<SecretKey>())
            .Verifiable();
        MockSecretKeyCollectionProviderFactory
            .Setup(x => x.CreateStatic(It.IsAny<IEnumerable<SecretKey>>()))
            .Returns(mockProvider.Object)
            .Verifiable();
        MockResourceServerStore
            .Setup(x => x.GetOrDefaultAsync("rs-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceServer("rs-api", "read:x", "write:x"))
            .Verifiable();

        await using var resolution = await Resolver.ResolveByIdAsync(
            MockContext.Object,
            "rs-api",
            CancellationToken.None
        );

        var single = resolution.Single;
        Assert.NotNull(single);
        Assert.Equal("urn:rs-api", single.Identifier);
        Assert.Equal(["read:x", "write:x"], single.Scopes);
        Assert.False(single.IsSystem);
        Assert.Same(mockProvider.Object, single.SecretsProvider);
    }

    [Fact]
    public async Task ResolveByIdAsync_WhenTheResourceServerIsDisabled_ResolvesNone()
    {
        SetupContext();
        SetupStore();
        var resourceServer = ResourceServer("rs-api", "read:x");
        resourceServer.IsDisabled = true;
        MockResourceServerStore
            .Setup(x => x.GetOrDefaultAsync("rs-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resourceServer)
            .Verifiable();

        await using var resolution = await Resolver.ResolveByIdAsync(
            MockContext.Object,
            "rs-api",
            CancellationToken.None
        );

        Assert.Empty(resolution.ResourceServers);
    }

    [Fact]
    public async Task ResolveByIdAsync_WhenTheResourceServerIsUnknown_ResolvesNone()
    {
        SetupContext();
        SetupStore();
        MockResourceServerStore
            .Setup(x => x.GetOrDefaultAsync("rs-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedResourceServer?)null)
            .Verifiable();

        await using var resolution = await Resolver.ResolveByIdAsync(
            MockContext.Object,
            "rs-api",
            CancellationToken.None
        );

        Assert.Empty(resolution.ResourceServers);
    }

    private void SetupErrorFactory()
    {
        var mockErrorFactory = new Mock<IOpenIdErrorFactory>(MockBehavior.Loose);
        var mockError = new Mock<IOpenIdError>(MockBehavior.Loose);
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }
}
