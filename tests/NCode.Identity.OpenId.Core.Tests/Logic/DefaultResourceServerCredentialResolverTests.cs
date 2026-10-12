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
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Logic;

public sealed class DefaultResourceServerCredentialResolverTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IResourceServerStore> MockResourceServerStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISecretSerializer> MockSecretSerializer { get; }
    private Mock<ISecretKeyCollectionProviderFactory> MockSecretKeyCollectionProviderFactory { get; }

    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdTenant> MockTenant { get; }
    private Mock<OpenIdEnvironment> MockEnvironment { get; }
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
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockSecretSerializer = MockRepository.Create<ISecretSerializer>();
        MockSecretKeyCollectionProviderFactory =
            MockRepository.Create<ISecretKeyCollectionProviderFactory>();

        MockContext = MockRepository.Create<OpenIdContext>();
        MockTenant = MockRepository.Create<OpenIdTenant>();
        MockEnvironment = MockRepository.Create<OpenIdEnvironment>();
        MockSettingsProvider = MockRepository.Create<IReadOnlySettingCollectionProvider>();
        MockTenantSettings = MockRepository.Create<IReadOnlySettingCollection>();

        Resolver = new DefaultResourceServerCredentialResolver(
            MockStoreManagerFactory.Object,
            MockAmbientTenantAccessor.Object,
            MockSettingSerializer.Object,
            MockSecretSerializer.Object,
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
        MockContext.SetupGet(x => x.Environment).Returns(MockEnvironment.Object).Verifiable();
        MockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        MockTenant
            .SetupGet(x => x.SettingsProvider)
            .Returns(MockSettingsProvider.Object)
            .Verifiable();
        MockSettingsProvider
            .SetupGet(x => x.Collection)
            .Returns(MockTenantSettings.Object)
            .Verifiable();
        MockEnvironment
            .SetupGet(x => x.JsonSerializerOptions)
            .Returns(JsonSerializerOptions.Default)
            .Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(Mock.Of<IDisposable>())
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

    // The merge of tenant-effective settings with each resource server's deserialized settings yields the effective
    // settings carried on each resolved resource server; the tests do not read those values.
    private void SetupSettingsMerge()
    {
        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(It.IsAny<JsonElement>(), It.IsAny<JsonSerializerOptions>())
            )
            .Returns(Array.Empty<Setting>())
            .Verifiable();
        MockTenantSettings
            .Setup(x => x.Merge(It.IsAny<IEnumerable<Setting>>()))
            .Returns(MockRepository.Create<ISettingCollection>().Object)
            .Verifiable();
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
        Assert.Null(resolution.SingleSecretsProvider);
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
        Assert.Null(resolution.SingleSecretsProvider);
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
    public async Task ResolveAsync_SingleMatch_SetsSingleAndBuildsSecretsProvider()
    {
        SetupContext();
        SetupStore();
        SetupSettingsMerge();
        SetupFindEnabledByScopes(ResourceServer("rs-api", "read:x"));

        var mockProvider = MockRepository.Create<ISecretKeyCollectionProvider>();
        mockProvider.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockSecretSerializer
            .Setup(x => x.DeserializeSecrets(It.IsAny<IReadOnlyCollection<PersistedSecret>>()))
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
        Assert.NotNull(resolution.Single);
        Assert.Equal("rs-api", resolution.Single!.ResourceServerId);
        Assert.Same(mockProvider.Object, resolution.SingleSecretsProvider);
        Assert.Equal(["urn:rs-api"], resolution.Audiences);
    }

    [Fact]
    public async Task ResolveAsync_MultipleMatches_SingleNullAndAudiencesSorted()
    {
        SetupContext();
        SetupStore();
        SetupSettingsMerge();
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
        Assert.Null(resolution.SingleSecretsProvider);
        // Audiences are ordered by identifier (urn:rs-a before urn:rs-b).
        Assert.Equal(["urn:rs-a", "urn:rs-b"], resolution.Audiences);
    }

    [Fact]
    public async Task ResolveAsync_RequestedResourceNarrowsAudience()
    {
        SetupContext();
        SetupStore();
        SetupSettingsMerge();
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

    private void SetupErrorFactory()
    {
        var mockErrorFactory = new Mock<IOpenIdErrorFactory>(MockBehavior.Loose);
        var mockError = new Mock<IOpenIdError>(MockBehavior.Loose);
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }
}
