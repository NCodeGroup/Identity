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
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.ConformanceEnvironment;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ConformanceSeedHandlerTests : SecretProtectionTestBase
{
    private const string WorkloadTenantId = "default";
    private const string SecretText = "conformance-secret-value";
    private const string GeneratedSecretId = "generated-secret-id";

    // The option holds the Base64Url of the secret's UTF-8 bytes.
    private static readonly string EncodedSecret = Base64Url.EncodeToString(
        Encoding.UTF8.GetBytes(SecretText)
    );

    private ConformanceSeedHandler CreateHandler(ConformanceSeedOptions options) =>
        CreateHandler(
            options,
            CreateStrictMock<ISettingSerializer>().Object,
            CreateStrictMock<IOpenIdEnvironmentProvider>().Object
        );

    private ConformanceSeedHandler CreateHandler(
        ConformanceSeedOptions options,
        ISettingSerializer settingSerializer,
        IOpenIdEnvironmentProvider openIdEnvironmentProvider,
        IClientKeyImporter? clientKeyImporter = null,
        CoherencePolicy? coherencePolicy = null,
        SeededLocalAccountValidator? seededLocalAccountValidator = null
    )
    {
        // Strict mocks: on the guard paths under test the secret, protector, crypto, and time dependencies are
        // untouched, and a client without a secret never loads the tenant's protector.
        var mockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        var mockSecretProtectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        var mockCryptoService = CreateStrictMock<ICryptoService>();

        coherencePolicy ??= new CoherencePolicy(
            CreateStrictMock<IEffectiveSettingsResolver>().Object,
            CreateStrictMock<IClientCoherenceValidator>().Object
        );
        seededLocalAccountValidator ??= new SeededLocalAccountValidator(
            CreateStrictMock<IEffectiveSettingsResolver>().Object,
            CreateStrictMock<IPasswordPolicy>().Object
        );

        return new ConformanceSeedHandler(
            options,
            mockSecretGenerator.Object,
            mockSecretProtectorProvider.Object,
            clientKeyImporter ?? CreateStrictMock<IClientKeyImporter>().Object,
            mockCryptoService.Object,
            TimeProvider.System,
            new SeededClientValidator(
                settingSerializer,
                openIdEnvironmentProvider,
                coherencePolicy.Resolver,
                coherencePolicy.Validator
            ),
            seededLocalAccountValidator,
            NullLogger<ConformanceSeedHandler>.Instance
        );
    }

    private ConformanceSeedHandler CreateHandler(
        ConformanceSeedOptions options,
        ISettingSerializer settingSerializer,
        IOpenIdEnvironmentProvider openIdEnvironmentProvider,
        CoherencePolicy coherencePolicy,
        ServiceProvider secretServices
    )
    {
        // The real generator and protector provider, because the generator's ImportSharedSecret takes a span that a
        // Moq mock cannot set up. The crypto service hands out the id of the one secret that the seed path creates.
        var mockCryptoService = CreateStrictMock<ICryptoService>();
        mockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns(GeneratedSecretId)
            .Verifiable();

        return new ConformanceSeedHandler(
            options,
            secretServices.GetRequiredService<ISecretGenerator>(),
            secretServices.GetRequiredService<ISecretProtectorProvider>(),
            CreateStrictMock<IClientKeyImporter>().Object,
            mockCryptoService.Object,
            TimeProvider.System,
            new SeededClientValidator(
                settingSerializer,
                openIdEnvironmentProvider,
                coherencePolicy.Resolver,
                coherencePolicy.Validator
            ),
            new SeededLocalAccountValidator(
                CreateStrictMock<IEffectiveSettingsResolver>().Object,
                CreateStrictMock<IPasswordPolicy>().Object
            ),
            NullLogger<ConformanceSeedHandler>.Instance
        );
    }

    private sealed record CoherencePolicy(
        IEffectiveSettingsResolver Resolver,
        IClientCoherenceValidator Validator
    );

    private CoherencePolicy CreateCoherencePolicy(params ClientCoherenceViolation[] violations)
    {
        var mockCollection = CreateLooseMock<ISettingCollection>();

        var mockResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        mockResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCollection.Object)
            .Verifiable();
        mockResolver
            .Setup(x =>
                x.Resolve(
                    It.IsAny<IReadOnlySettingCollection>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(mockCollection.Object)
            .Verifiable();

        var mockValidator = CreateStrictMock<IClientCoherenceValidator>();
        mockValidator
            .Setup(x => x.Validate(mockCollection.Object, It.IsAny<JsonElement>()))
            .Returns(violations)
            .Verifiable();

        return new CoherencePolicy(mockResolver.Object, mockValidator.Object);
    }

    private SeededLocalAccountValidator CreateSeededLocalAccountValidator(
        PasswordPolicyResult result
    )
    {
        var mockCollection = CreateLooseMock<ISettingCollection>();

        var mockResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        mockResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCollection.Object)
            .Verifiable();
        mockResolver
            .Setup(x =>
                x.Resolve(
                    It.IsAny<IReadOnlySettingCollection>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(mockCollection.Object)
            .Verifiable();

        var mockPolicy = CreateStrictMock<IPasswordPolicy>();
        mockPolicy
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<PasswordPolicyContext>(),
                    It.IsAny<ReadOnlyMemory<char>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

        return new SeededLocalAccountValidator(mockResolver.Object, mockPolicy.Object);
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

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNothingConfiguredToSeed_DoesNothing()
    {
        var handler = CreateHandler(
            new ConformanceSeedOptions { Clients = [], TestUserPassword = null }
        );

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNoTenantIsNamed_DoesNothing()
    {
        var handler = CreateHandler(
            new ConformanceSeedOptions { TestUserPassword = "conformance-password" }
        );

        await handler.HandleAsync(CreateCommand("root", TenantPlane.Root), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantDoesNotMatchConfiguredTarget_DoesNothing()
    {
        var handler = CreateHandler(
            new ConformanceSeedOptions
            {
                TestUserPassword = "conformance-password",
                TenantId = "tenant-a",
            }
        );

        await handler.HandleAsync(
            CreateCommand("tenant-b", TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTestUserConfigured_SeedsAccountWithProfileClaims()
    {
        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients = [],
            TestUserName = "conformance-user",
            TestUserEmail = "conformance-user@localhost",
            TestUserGivenName = "Conformance",
            TestUserFamilyName = "User",
            TestUserPassword = "conformance-password",
        };
        var handler = CreateHandler(
            options,
            CreateStrictMock<ISettingSerializer>().Object,
            CreateStrictMock<IOpenIdEnvironmentProvider>().Object,
            seededLocalAccountValidator: CreateSeededLocalAccountValidator(
                PasswordPolicyResult.Accepted
            )
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockAccountStore = CreateStrictMock<ILocalAccountStore>();
        mockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(mockAccountStore.Object)
            .Verifiable();
        mockAccountStore
            .Setup(x =>
                x.GetByUserNameOrDefaultAsync("conformance-user", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedLocalAccount?)null)
            .Verifiable();

        PersistedLocalAccount? captured = null;
        mockAccountStore
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<PersistedLocalAccount>(),
                    It.IsAny<ReadOnlyMemory<byte>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (PersistedLocalAccount account, ReadOnlyMemory<byte>? _, CancellationToken _) =>
                    captured = account
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("conformance-user@localhost", captured.Email);
        Assert.True(captured.EmailVerified);

        var claims = captured.Claims.ToDictionary(claim => claim.Type, claim => claim.Value);
        Assert.Equal("Conformance", claims[JoseClaimNames.Payload.GivenName]);
        Assert.Equal("User", claims[JoseClaimNames.Payload.FamilyName]);
        Assert.Equal("Conformance User", claims[JoseClaimNames.Payload.Name]);
    }

    [Fact]
    public async Task HandleAsync_WhenTheTestUserPasswordIsRefused_ThrowsNamingTheTestUser()
    {
        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients = [],
            TestUserName = "conformance-user",
            TestUserPassword = "short",
        };
        var handler = CreateHandler(
            options,
            CreateStrictMock<ISettingSerializer>().Object,
            CreateStrictMock<IOpenIdEnvironmentProvider>().Object,
            seededLocalAccountValidator: CreateSeededLocalAccountValidator(
                PasswordPolicyResult.Rejected(
                    PasswordPolicyErrors.TooShort,
                    "The password must have at least 15 characters."
                )
            )
        );

        // No AddAsync setup: the strict store refuses an account that would be stored.
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockAccountStore = CreateStrictMock<ILocalAccountStore>();
        mockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(mockAccountStore.Object)
            .Verifiable();
        mockAccountStore
            .Setup(x =>
                x.GetByUserNameOrDefaultAsync("conformance-user", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedLocalAccount?)null)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'conformance-user'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenClientSettingsCannotBeHonored_ThrowsNamingTheClient()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Returns([
                new SettingError(
                    "grant_types_supported",
                    "The setting 'grant_types_supported' must list only grant types."
                ),
            ])
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "client",
                    ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0LWNsaWVudA",
                    RedirectUris = ["https://client.example/callback"],
                    GrantTypes = ["authorisation_code"],
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'client'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'grant_types_supported'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenClientConfigured_RegistersItsMethodAndAppliesDefaults()
    {
        // The registered token_endpoint_auth_method decides the client type, so a seeded client is confidential the
        // way a dynamically registered one is, and it is the only method the client may use — the registration alone
        // says so, without a per-client token_endpoint_auth_methods_supported. Unconfigured lists take their
        // defaults: the Basic OP grant types, and the server's PKCE methods (the setting is left out).
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        // The validation result stops the seeding before the secret is stored, once the document is captured.
        JsonElement? capturedSettings = null;
        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Callback(
                (JsonElement settings, string _, JsonSerializerOptions _) =>
                    capturedSettings = settings
            )
            .Returns([new SettingError("redirect_uris", "stop")])
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "client",
                    ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0LWNsaWVudA",
                    RedirectUris = ["https://client.example/callback"],
                    TokenEndpointAuthMethod = "client_secret_post",
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.NotNull(capturedSettings);
        var settingsDocument = capturedSettings.Value;
        Assert.Equal(
            "client_secret_post",
            settingsDocument.GetProperty("token_endpoint_auth_method").GetString()
        );
        Assert.False(
            settingsDocument.TryGetProperty("token_endpoint_auth_methods_supported", out _)
        );
        Assert.Equal(
            ["authorization_code", "refresh_token"],
            GetStrings(settingsDocument, "grant_types_supported")
        );
        Assert.False(settingsDocument.TryGetProperty("code_challenge_methods_supported", out _));
    }

    [Fact]
    public async Task HandleAsync_WhenClientHasOnlyKeySet_StoresImportedKeysAndProfiles()
    {
        // A FAPI client authenticates with its keys (private_key_jwt or self_signed_tls_client_auth), so it is seeded
        // without a secret: its credentials are exactly the keys the client-key policy imported from its key set.
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Returns([])
            .Verifiable();

        var importedKey = new PersistedSecret
        {
            SecretId = "key-1",
            KeyId = "key-1",
            Use = null,
            Algorithm = "ES256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
            SecretType = "ecc",
            SecretEncoding = "spki",
            KeySizeBits = 256,
            EncodedValue = "public-key",
        };
        const string jwks = """{"keys":[{"kty":"EC","kid":"key-1"}]}""";
        var mockKeyImporter = CreateStrictMock<IClientKeyImporter>();
        IReadOnlyList<PersistedSecret>? importedKeys = [importedKey];
        string? importError = null;
        mockKeyImporter
            .Setup(x =>
                x.TryImportRegisteredKeySet(
                    It.Is<JsonElement>(keySet => keySet.GetRawText() == jwks),
                    It.IsAny<DateTimeOffset>(),
                    out importedKeys,
                    out importError
                )
            )
            .Returns(true)
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "fapi1",
                    Jwks = jwks,
                    RedirectUris = ["https://client.example/callback"],
                    TokenEndpointAuthMethod = "private_key_jwt",
                    SecurityProfiles = ["fapi1-advanced"],
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object,
            mockKeyImporter.Object,
            CreateCoherencePolicy()
        );

        PersistedClient? storedClient = null;
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("fapi1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        mockClientStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedClient client, CancellationToken _) => storedClient = client)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
            CancellationToken.None
        );

        Assert.NotNull(storedClient);
        Assert.Equal([importedKey], storedClient.Secrets.Value);
        var settingsDocument = storedClient.Settings.Value;
        Assert.Equal(
            "private_key_jwt",
            settingsDocument.GetProperty("token_endpoint_auth_method").GetString()
        );
        Assert.Equal(["fapi1-advanced"], GetStrings(settingsDocument, "security_profiles"));
    }

    [Fact]
    public async Task HandleAsync_WhenKeySetIsRefused_ThrowsNamingTheClient()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Returns([])
            .Verifiable();

        var mockKeyImporter = CreateStrictMock<IClientKeyImporter>();
        IReadOnlyList<PersistedSecret>? importedKeys = null;
        string? importError = "The key 'key-1' is a private key.";
        mockKeyImporter
            .Setup(x =>
                x.TryImportRegisteredKeySet(
                    It.IsAny<JsonElement>(),
                    It.IsAny<DateTimeOffset>(),
                    out importedKeys,
                    out importError
                )
            )
            .Returns(false)
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "fapi1",
                    Jwks = """{"keys":[{"kty":"EC","kid":"key-1","d":"private"}]}""",
                    RedirectUris = ["https://client.example/callback"],
                    TokenEndpointAuthMethod = "private_key_jwt",
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object,
            mockKeyImporter.Object,
            CreateCoherencePolicy()
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("fapi1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'fapi1'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("is a private key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenTheClientSettingsAreIncoherent_ThrowsNamingTheClient()
    {
        const string redirectUri = "http://client.example/callback";

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Returns([])
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "client",
                    ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0LWNsaWVudA",
                    RedirectUris = [redirectUri],
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object,
            coherencePolicy: CreateCoherencePolicy(
                new ClientCoherenceViolation(
                    OpenIdSettingNames.RedirectUris,
                    redirectUri,
                    OpenIdSettingNames.RedirectUriKindsSupported
                )
            )
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'client'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(redirectUri, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenClientHasASecret_StoresItProtectedUnderTheTenantsMasterKey()
    {
        // The settings pass the validation and the coherence rules, so the seeding reaches the secret: it is imported
        // through the real generator and protected with the master key of the tenant that the client is seeded into.
        var owner = SecretOwner.ForTenant(WorkloadTenantId);
        var secretServices = CreateSecretServices(owner);

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Returns([])
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "client",
                    ClientSecret = EncodedSecret,
                    RedirectUris = ["https://client.example/callback"],
                    TokenEndpointAuthMethod = "client_secret_post",
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object,
            CreateCoherencePolicy(),
            secretServices
        );

        PersistedClient? storedClient = null;
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        mockClientStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedClient client, CancellationToken _) => storedClient = client)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
            CancellationToken.None
        );

        Assert.NotNull(storedClient);
        Assert.Equal(WorkloadTenantId, storedClient.TenantId);
        Assert.Equal("client", storedClient.ClientId);
        var secret = Assert.Single(storedClient.Secrets.Value);
        await AssertSecretIsProtectedUnderTenantAsync(
            secretServices,
            owner,
            secret,
            GeneratedSecretId,
            SecretText
        );
    }

    private static IEnumerable<string?> GetStrings(JsonElement document, string propertyName) =>
        document.GetProperty(propertyName).EnumerateArray().Select(element => element.GetString());

    #endregion
}
