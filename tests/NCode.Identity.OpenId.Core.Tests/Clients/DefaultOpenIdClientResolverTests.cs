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
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultOpenIdClientResolverTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string ResourceServerId = "rs-1";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IOpenIdClientFactory> MockClientFactory { get; }
    private Mock<ISecretSerializer> MockSecretSerializer { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdTenant> MockTenant { get; }
    private DefaultHttpContext HttpContext { get; } = new();
    private DefaultOpenIdClientResolver Resolver { get; }

    public DefaultOpenIdClientResolverTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockClientStore = CreateStrictMock<IClientStore>();
        MockClientFactory = CreateStrictMock<IOpenIdClientFactory>();
        MockSecretSerializer = CreateStrictMock<ISecretSerializer>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockTenant = CreateStrictMock<OpenIdTenant>();

        MockContext.SetupGet(x => x.Tenant).Returns(MockTenant.Object).Verifiable();
        MockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        Resolver = new DefaultOpenIdClientResolver(
            MockStoreManagerFactory.Object,
            MockClientFactory.Object,
            MockSecretSerializer.Object,
            MockEffectiveSettingsResolver.Object
        );
    }

    #region Scaffolding

    private static PersistedClient CreatePersistedClient(
        string? resourceServerId = null,
        string tenantId = TenantId,
        bool isDisabled = false
    ) =>
        new()
        {
            TenantId = tenantId,
            ClientId = ClientId,
            ConcurrencyToken = "ct",
            IsDisabled = isDisabled,
            ResourceServerId = resourceServerId,
            Settings = new PersistedClientSettings
            {
                TenantId = tenantId,
                ClientId = ClientId,
                ConcurrencyToken = "ct",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = tenantId,
                ClientId = ClientId,
                ConcurrencyToken = "ct",
                Value = [],
            },
        };

    private void SetupStoredClient(PersistedClient? persistedClient) =>
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(persistedClient)
            .Verifiable();

    private void SetupHttpContext() =>
        MockContext.SetupGet(x => x.Http).Returns(HttpContext).Verifiable();

    private void SetupEndpoint(params object[] metadata) =>
        HttpContext.SetEndpoint(
            new Endpoint(null, new EndpointMetadataCollection(metadata), "test")
        );

    // Building the client expands its settings over the tenant's and deserializes its secrets.
    private OpenIdClient SetupClientCreation(string? expectedResourceServerId)
    {
        var mockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        var mockTenantSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var effectiveSettings = CreateLooseMock<ISettingCollection>().Object;
        var client = CreateStrictMock<OpenIdClient>().Object;

        MockTenant
            .SetupGet(x => x.SettingsProvider)
            .Returns(mockSettingsProvider.Object)
            .Verifiable();
        mockSettingsProvider
            .SetupGet(x => x.Collection)
            .Returns(mockTenantSettings.Object)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    mockTenantSettings.Object,
                    OpenIdSettingScopes.Client,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(effectiveSettings)
            .Verifiable();
        MockSecretSerializer
            .Setup(x => x.DeserializeSecrets(It.IsAny<IReadOnlyCollection<PersistedSecret>>()))
            .Returns(Array.Empty<SecretKey>())
            .Verifiable();
        MockClientFactory
            .Setup(x =>
                x.CreatePublicClientAsync(
                    MockContext.Object,
                    It.Is<OpenIdClientDefinition>(definition =>
                        definition.ClientId == ClientId
                        && definition.ResourceServerId == expectedResourceServerId
                        && ReferenceEquals(definition.Settings, effectiveSettings)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(client)
            .Verifiable();

        return client;
    }

    private async Task<OpenIdClient?> ResolveAsync() =>
        await Resolver.ResolveClientOrDefaultAsync(
            MockContext.Object,
            ClientId,
            CancellationToken.None
        );

    #endregion

    #region ResolveClientOrDefaultAsync Tests

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenTheClientIsUnknown_ReturnsNull()
    {
        SetupStoredClient(null);

        var client = await ResolveAsync();

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenTheClientIsDisabled_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(isDisabled: true));

        var client = await ResolveAsync();

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenTheClientBelongsToAnotherTenant_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(tenantId: "tenant-2"));

        var client = await ResolveAsync();

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenAnOrdinaryClient_ResolvesItAtAnyEndpoint()
    {
        SetupStoredClient(CreatePersistedClient());
        var expected = SetupClientCreation(expectedResourceServerId: null);

        var client = await ResolveAsync();

        Assert.Same(expected, client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenABoundClientOutsideAnEndpoint_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(ResourceServerId));
        SetupHttpContext();

        var client = await ResolveAsync();

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenABoundClientAtAnEndpointThatDoesNotAcceptIt_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(ResourceServerId));
        SetupHttpContext();
        SetupEndpoint();

        var client = await ResolveAsync();

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenABoundClientAtAnEndpointThatOptsOut_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(ResourceServerId));
        SetupHttpContext();
        SetupEndpoint(new ResourceServerClientMetadata(false));

        var client = await ResolveAsync();

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenABoundClientAtAnEndpointThatAcceptsIt_ResolvesItWithItsBinding()
    {
        SetupStoredClient(CreatePersistedClient(ResourceServerId));
        SetupHttpContext();
        SetupEndpoint(new ResourceServerClientMetadata(true));
        var expected = SetupClientCreation(ResourceServerId);

        var client = await ResolveAsync();

        Assert.Same(expected, client);
    }

    #endregion

    private sealed class ResourceServerClientMetadata(bool accepts)
        : IOpenIdEndpointResourceServerClientMetadata
    {
        public bool AcceptsResourceServerClients { get; } = accepts;
    }
}
