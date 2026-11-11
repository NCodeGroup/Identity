// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Verifiers;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultOpenIdClientResolverTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string PrincipalId = "principal-1";
    private const string ResourceServerId = "rs-1";

    private static DateTimeOffset Now { get; } = DateTimeOffset.UnixEpoch.AddYears(50);

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IOpenIdClientFactory> MockClientFactory { get; }
    private Mock<ISecretSerializer> MockSecretSerializer { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdTenant> MockTenant { get; }
    private DefaultHttpContext HttpContext { get; } = new();
    private OpenIdClientDefinition? CreatedDefinition { get; set; }
    private DefaultOpenIdClientResolver Resolver { get; }

    public DefaultOpenIdClientResolverTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockClientStore = CreateStrictMock<IClientStore>();
        MockClientFactory = CreateStrictMock<IOpenIdClientFactory>();
        MockSecretSerializer = CreateStrictMock<ISecretSerializer>();
        MockSecretProtectorProvider = CreateStrictMock<ISecretProtectorProvider>();
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
            MockSecretProtectorProvider.Object,
            MockEffectiveSettingsResolver.Object,
            TimeProvider
        );
    }

    #region Scaffolding

    private static PersistedClient CreatePersistedClient(
        string? resourceServerId = null,
        string tenantId = TenantId,
        bool isDisabled = false,
        IReadOnlyCollection<PersistedSecret>? secrets = null
    ) =>
        new PersistedClientBuilder
        {
            TenantId = tenantId,
            ClientId = ClientId,
            PrincipalId = PrincipalId,
            IsDisabled = isDisabled,
            ResourceServerId = resourceServerId,
            Secrets = secrets ?? [],
        }.Build();

    private static PersistedSecret CreatePersistedSecret(
        string secretId,
        DateTimeOffset expiresWhen,
        string secretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw
    ) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            ExpiresWhen = expiresWhen,
            RetiresWhen = expiresWhen,
            SecretEncoding = secretEncoding,
            EncodedValue = "encoded",
        }.Build();

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

    // Building the client expands its settings over the tenant's and deserializes the secrets with the specified
    // identifiers, in their stored order.
    private OpenIdClient SetupClientCreation(
        string? expectedResourceServerId,
        params string[] expectedSecretIds
    ) => SetupClientCreation(expectedResourceServerId, expectedSecretIds, [], []);

    // Building the client also reads the secrets with the specified identifiers as verifiers, in their stored order,
    // and gives the client the verifiers they produce.
    private OpenIdClient SetupClientCreation(
        string? expectedResourceServerId,
        string[] expectedSecretIds,
        string[] expectedVerifierIds,
        IReadOnlyCollection<SecretVerifier> verifiers
    )
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
        var protector = CreateStrictMock<ISecretProtector>().Object;
        MockSecretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant(TenantId),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(protector)
            .Verifiable();
        MockSecretSerializer
            .Setup(x =>
                x.DeserializeSecrets(
                    protector,
                    It.Is<IReadOnlyCollection<PersistedSecret>>(secrets =>
                        secrets.Select(secret => secret.SecretId).SequenceEqual(expectedSecretIds)
                    )
                )
            )
            .Returns(Array.Empty<SecretKey>())
            .Verifiable();
        MockSecretSerializer
            .Setup(x =>
                x.DeserializeVerifiers(
                    protector,
                    It.Is<IReadOnlyCollection<PersistedSecret>>(secrets =>
                        secrets.Select(secret => secret.SecretId).SequenceEqual(expectedVerifierIds)
                    )
                )
            )
            .Returns(verifiers)
            .Verifiable();
        MockClientFactory
            .Setup(x =>
                x.CreatePublicClientAsync(
                    MockContext.Object,
                    It.Is<OpenIdClientDefinition>(definition =>
                        definition.ClientId == ClientId
                        && definition.ResourceServerId == expectedResourceServerId
                        && ReferenceEquals(definition.Settings, effectiveSettings)
                        && ReferenceEquals(definition.SecretVerifiers, verifiers)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<OpenIdContext, OpenIdClientDefinition, CancellationToken>(
                (_, definition, _) => CreatedDefinition = definition
            )
            .ReturnsAsync(client)
            .Verifiable();

        return client;
    }

    private async ValueTask<OpenIdClient?> ResolveAsync(CancellationToken cancellationToken) =>
        await Resolver.ResolveClientOrDefaultAsync(MockContext.Object, ClientId, cancellationToken);

    #endregion

    #region ResolveClientOrDefaultAsync Tests

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenTheClientIsUnknown_ReturnsNull()
    {
        SetupStoredClient(null);

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenTheClientIsDisabled_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(isDisabled: true));

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenTheClientBelongsToAnotherTenant_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(tenantId: "tenant-2"));

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenAnOrdinaryClient_ResolvesItAtAnyEndpoint()
    {
        SetupStoredClient(CreatePersistedClient());
        var expected = SetupClientCreation(expectedResourceServerId: null);

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Same(expected, client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenTheClientExists_PassesItsPrincipalToTheFactory()
    {
        SetupStoredClient(CreatePersistedClient());
        SetupClientCreation(expectedResourceServerId: null);

        await ResolveAsync(CancellationToken.None);

        Assert.NotNull(CreatedDefinition);
        Assert.Equal(PrincipalId, CreatedDefinition.PrincipalId);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenASecretHasExpired_DeserializesOnlyTheUnexpiredSecrets()
    {
        var expired = CreatePersistedSecret("expired", Now);
        var live = CreatePersistedSecret("live", Now.AddDays(1));
        SetupStoredClient(CreatePersistedClient(secrets: [expired, live]));
        var expected = SetupClientCreation(expectedResourceServerId: null, "live");

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Same(expected, client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenNoSecretHasExpired_DeserializesEverySecret()
    {
        var first = CreatePersistedSecret("first", Now.AddTicks(1));
        var second = CreatePersistedSecret("second", Now.AddDays(1));
        SetupStoredClient(CreatePersistedClient(secrets: [first, second]));
        var expected = SetupClientCreation(expectedResourceServerId: null, "first", "second");

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Same(expected, client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenEverySecretHasExpired_DeserializesNone()
    {
        var first = CreatePersistedSecret("first", Now);
        var second = CreatePersistedSecret("second", Now.AddDays(-1));
        SetupStoredClient(CreatePersistedClient(secrets: [first, second]));
        var expected = SetupClientCreation(expectedResourceServerId: null);

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Same(expected, client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenTimeAdvancesPastASecretsExpiry_DeserializesOnlyTheUnexpiredSecrets()
    {
        var expiring = CreatePersistedSecret("expiring", Now.AddMinutes(5));
        var live = CreatePersistedSecret("live", Now.AddDays(1));
        SetupStoredClient(CreatePersistedClient(secrets: [expiring, live]));
        var expected = SetupClientCreation(expectedResourceServerId: null, "live");
        TimeProvider.Advance(TimeSpan.FromMinutes(5));

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Same(expected, client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenABoundClientOutsideAnEndpoint_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(ResourceServerId));
        SetupHttpContext();

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_ReadsTheVerifiersApartFromTheKeys()
    {
        // A one-way verifier is not key material, so it is read apart from the client's keys. See ADR-1391.
        var key = CreatePersistedSecret("key", Now.AddDays(1));
        var firstVerifier = CreatePersistedSecret(
            "verifier-1",
            Now.AddDays(1),
            NCode.Identity.Secrets.SecretEncodings.Verifier
        );
        var secondVerifier = CreatePersistedSecret(
            "verifier-2",
            Now.AddDays(2),
            NCode.Identity.Secrets.SecretEncodings.Verifier
        );
        SetupStoredClient(CreatePersistedClient(secrets: [firstVerifier, key, secondVerifier]));
        SecretVerifier[] verifiers = [CreateLooseMock<SecretVerifier>().Object];
        var expected = SetupClientCreation(
            expectedResourceServerId: null,
            ["key"],
            ["verifier-1", "verifier-2"],
            verifiers
        );

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Same(expected, client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenEverySecretIsAVerifier_ReadsNoKeys()
    {
        var verifier = CreatePersistedSecret(
            "verifier",
            Now.AddDays(1),
            NCode.Identity.Secrets.SecretEncodings.Verifier
        );
        var expiredVerifier = CreatePersistedSecret(
            "expired",
            Now,
            NCode.Identity.Secrets.SecretEncodings.Verifier
        );
        SetupStoredClient(CreatePersistedClient(secrets: [expiredVerifier, verifier]));
        var expected = SetupClientCreation(expectedResourceServerId: null, [], ["verifier"], []);

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Same(expected, client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenABoundClientAtAnEndpointThatDoesNotAcceptIt_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(ResourceServerId));
        SetupHttpContext();
        SetupEndpoint();

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenABoundClientAtAnEndpointThatOptsOut_ReturnsNull()
    {
        SetupStoredClient(CreatePersistedClient(ResourceServerId));
        SetupHttpContext();
        SetupEndpoint(new ResourceServerClientMetadata(false));

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Null(client);
    }

    [Fact]
    public async Task ResolveClientOrDefaultAsync_WhenABoundClientAtAnEndpointThatAcceptsIt_ResolvesItWithItsBinding()
    {
        SetupStoredClient(CreatePersistedClient(ResourceServerId));
        SetupHttpContext();
        SetupEndpoint(new ResourceServerClientMetadata(true));
        var expected = SetupClientCreation(ResourceServerId);

        var client = await ResolveAsync(CancellationToken.None);

        Assert.Same(expected, client);
    }

    #endregion

    private sealed class ResourceServerClientMetadata(bool accepts)
        : IOpenIdEndpointResourceServerClientMetadata
    {
        public bool AcceptsResourceServerClients { get; } = accepts;
    }
}
