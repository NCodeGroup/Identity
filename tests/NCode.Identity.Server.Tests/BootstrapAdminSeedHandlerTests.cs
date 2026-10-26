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

using System.Buffers.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Buffers;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class BootstrapAdminSeedHandlerTests : SecretProtectionTestBase
{
    private const string ClientId = "bootstrap-admin";
    private const string ClientSecret = "secret-value";
    private const string WorkloadTenantId = "default";
    private const string SecretText = "bootstrap-secret-value";
    private const string GeneratedSecretId = "generated-secret-id";
    private const string StaleSecretId = "stale-secret-id";

    // The option holds the Base64Url of the secret's UTF-8 bytes.
    private static readonly string EncodedSecret = Base64Url.EncodeToString(
        SecureEncoding.UTF8.GetBytes(SecretText)
    );

    private BootstrapAdminSeedHandler CreateHandler(
        BootstrapAdminOptions bootstrapOptions,
        SeededClientValidator? seededClientValidator = null
    )
    {
        // Strict mocks: none of these collaborators are touched on the no-op guard paths.
        var mockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        var mockSecretProtectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        var mockCryptoService = CreateStrictMock<ICryptoService>();
        var mockLogger = CreateLooseMock<ILogger<BootstrapAdminSeedHandler>>();

        return new BootstrapAdminSeedHandler(
            Options.Create(bootstrapOptions),
            mockSecretGenerator.Object,
            mockSecretProtectorProvider.Object,
            mockCryptoService.Object,
            TimeProvider.System,
            seededClientValidator ?? CreateStrictSeededClientValidator(),
            mockLogger.Object
        );
    }

    private BootstrapAdminSeedHandler CreateHandler(
        BootstrapAdminOptions bootstrapOptions,
        ServiceProvider secretServices,
        SeededClientValidator? seededClientValidator = null
    )
    {
        // The real generator and protector provider, because the generator's ImportSharedSecret takes a span that a
        // Moq mock cannot set up. The crypto service hands out the id of the one secret that the seed path creates.
        var mockCryptoService = CreateStrictMock<ICryptoService>();
        mockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns(GeneratedSecretId)
            .Verifiable();
        var mockLogger = CreateLooseMock<ILogger<BootstrapAdminSeedHandler>>();

        return new BootstrapAdminSeedHandler(
            Options.Create(bootstrapOptions),
            secretServices.GetRequiredService<ISecretGenerator>(),
            secretServices.GetRequiredService<ISecretProtectorProvider>(),
            mockCryptoService.Object,
            TimeProvider.System,
            seededClientValidator ?? CreateStrictSeededClientValidator(),
            mockLogger.Object
        );
    }

    // Strict mocks: none of the validator's collaborators is touched unless the seed reaches the check.
    private SeededClientValidator CreateStrictSeededClientValidator() =>
        new(
            CreateStrictMock<ISettingSerializer>().Object,
            CreateStrictMock<IOpenIdEnvironmentProvider>().Object,
            CreateStrictMock<IEffectiveSettingsResolver>().Object,
            CreateStrictMock<IClientCoherenceValidator>().Object
        );

    // A validator that accepts the client the seed creates: its settings are valid and no coherence rule refuses them.
    private SeededClientValidator CreateAcceptingSeededClientValidator()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(It.IsAny<JsonElement>(), OpenIdSettingScopes.Client, jsonOptions)
            )
            .Returns([])
            .Verifiable();

        var server = CreateLooseMock<ISettingCollection>().Object;
        var tenantSettings = CreateLooseMock<ISettingCollection>().Object;
        var mockResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        mockResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(server)
            .Verifiable();
        mockResolver
            .Setup(x => x.Resolve(server, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
            .Returns(tenantSettings)
            .Verifiable();

        var mockValidator = CreateStrictMock<IClientCoherenceValidator>();
        mockValidator
            .Setup(x => x.Validate(tenantSettings, It.IsAny<JsonElement>()))
            .Returns([])
            .Verifiable();

        return new SeededClientValidator(
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object,
            mockResolver.Object,
            mockValidator.Object
        );
    }

    private SeedTenantCommand CreateCommand(
        string tenantId,
        TenantPlane plane,
        IStoreManager? storeManager = null
    )
    {
        // Strict mock: the store manager is untouched on the guard paths under test.
        storeManager ??= CreateStrictMock<IStoreManager>().Object;

        var persistedTenant = new PersistedTenant
        {
            TenantId = tenantId,
            Issuer = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                Value = JsonElement.Parse("{}"),
            },
            Secrets = null!,
        };

        return new SeedTenantCommand(
            new TenantSeedContext
            {
                Tenant = persistedTenant,
                Plane = plane,
                StoreManager = storeManager,
            }
        );
    }

    private IStoreManager CreateStoreManager(IClientStore clientStore)
    {
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager.Setup(x => x.GetStore<IClientStore>()).Returns(clientStore).Verifiable();
        return mockStoreManager.Object;
    }

    private static BootstrapAdminOptions CreateConfiguredOptions() =>
        new()
        {
            ClientId = ClientId,
            ClientSecret = EncodedSecret,
            TenantId = WorkloadTenantId,
        };

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNotConfigured_DoesNothing()
    {
        var handler = CreateHandler(new BootstrapAdminOptions());

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNoTenantIsNamed_DoesNothing()
    {
        var handler = CreateHandler(
            new BootstrapAdminOptions { ClientId = ClientId, ClientSecret = ClientSecret }
        );

        await handler.HandleAsync(CreateCommand("root", TenantPlane.Root), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantDoesNotMatchConfiguredTarget_DoesNothing()
    {
        var handler = CreateHandler(
            new BootstrapAdminOptions
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                TenantId = "tenant-a",
            }
        );

        await handler.HandleAsync(
            CreateCommand("tenant-b", TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenClientIsNew_SeedsItWithTheSecretProtectedUnderTheTenantsMasterKey()
    {
        var owner = SecretOwner.ForTenant(WorkloadTenantId);
        var secretServices = CreateSecretServices(owner);
        var handler = CreateHandler(
            CreateConfiguredOptions(),
            secretServices,
            CreateAcceptingSeededClientValidator()
        );

        PersistedClient? addedClient = null;
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedClient?>((PersistedClient?)null))
            .Verifiable();
        mockClientStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedClient client, CancellationToken _) => addedClient = client)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await handler.HandleAsync(
            CreateCommand(
                WorkloadTenantId,
                TenantPlane.Workload,
                CreateStoreManager(mockClientStore.Object)
            ),
            CancellationToken.None
        );

        Assert.NotNull(addedClient);
        Assert.Equal(WorkloadTenantId, addedClient.TenantId);
        Assert.Equal(ClientId, addedClient.ClientId);
        var secret = Assert.Single(addedClient.Secrets.Value);
        await AssertSecretIsProtectedUnderTenantAsync(
            secretServices,
            owner,
            secret,
            GeneratedSecretId,
            SecretText
        );
    }

    [Fact]
    public async Task HandleAsync_WhenClientExists_ReplacesItsSecretUnderTheTenantsMasterKey()
    {
        var owner = SecretOwner.ForTenant(WorkloadTenantId);
        var secretServices = CreateSecretServices(owner);
        var handler = CreateHandler(CreateConfiguredOptions(), secretServices);

        // The handler only checks that the client exists; it works on the client's secrets through the store.
        var existingClient = new PersistedClient
        {
            TenantId = WorkloadTenantId,
            ClientId = ClientId,
            IsDisabled = false,
            Settings = null!,
            Secrets = null!,
        };
        var staleSecret = new PersistedSecret
        {
            SecretId = StaleSecretId,
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = SecretEncodings.SymmetricRaw,
            KeySizeBits = 8,
            EncodedValue = "nce1.stale",
        };
        var existingSecrets = new PersistedClientSecrets
        {
            TenantId = WorkloadTenantId,
            ClientId = ClientId,
            Value = [staleSecret],
        };

        PersistedSecret? addedSecret = null;
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedClient?>(existingClient))
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetSecretsOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedClientSecrets?>(existingSecrets))
            .Verifiable();
        mockClientStore
            .Setup(x => x.RemoveSecretAsync(ClientId, StaleSecretId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(true))
            .Verifiable();
        mockClientStore
            .Setup(x =>
                x.AddSecretAsync(
                    ClientId,
                    It.IsAny<PersistedSecret>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (string _, PersistedSecret secret, CancellationToken _) => addedSecret = secret
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await handler.HandleAsync(
            CreateCommand(
                WorkloadTenantId,
                TenantPlane.Workload,
                CreateStoreManager(mockClientStore.Object)
            ),
            CancellationToken.None
        );

        Assert.NotNull(addedSecret);
        await AssertSecretIsProtectedUnderTenantAsync(
            secretServices,
            owner,
            addedSecret,
            GeneratedSecretId,
            SecretText
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheClientWouldBeIncoherent_ThrowsWithoutStoringIt()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(It.IsAny<JsonElement>(), OpenIdSettingScopes.Client, jsonOptions)
            )
            .Returns([])
            .Verifiable();

        var server = CreateLooseMock<ISettingCollection>().Object;
        var tenantSettings = CreateLooseMock<ISettingCollection>().Object;
        var mockResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        mockResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(server)
            .Verifiable();
        mockResolver
            .Setup(x => x.Resolve(server, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
            .Returns(tenantSettings)
            .Verifiable();

        var mockValidator = CreateStrictMock<IClientCoherenceValidator>();
        mockValidator
            .Setup(x => x.Validate(tenantSettings, It.IsAny<JsonElement>()))
            .Returns([
                new ClientCoherenceViolation(
                    OpenIdSettingNames.TokenEndpointAuthMethod,
                    "client_secret_basic",
                    OpenIdSettingNames.TokenEndpointAuthMethodsSupported
                ),
            ])
            .Verifiable();

        var handler = CreateHandler(
            new BootstrapAdminOptions
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                TenantId = WorkloadTenantId,
            },
            new SeededClientValidator(
                mockSettingSerializer.Object,
                mockEnvironmentProvider.Object,
                mockResolver.Object,
                mockValidator.Object
            )
        );

        // No AddAsync setup: the strict store refuses a client that would be stored. The strict secret generator and
        // protector provider have no setup either, because the tenant's master key is loaded and the secret is imported
        // only after the client is accepted.
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'bootstrap-admin'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("client_secret_basic", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
