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
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DeveloperInteractiveSeedHandlerTests : BaseTests
{
    private const string WorkloadTenantId = "default";

    private DeveloperInteractiveSeedHandler CreateHandler(
        DeveloperInteractiveSeedOptions options
    ) => new(options, CreateSeededClientValidator(), CreateSeededLocalAccountValidator());

    private SeededClientValidator CreateSeededClientValidator(
        ISettingSerializer? settingSerializer = null,
        IOpenIdEnvironmentProvider? openIdEnvironmentProvider = null,
        IEffectiveSettingsResolver? effectiveSettingsResolver = null,
        IClientCoherenceValidator? clientCoherenceValidator = null
    ) =>
        new(
            settingSerializer ?? CreateStrictMock<ISettingSerializer>().Object,
            openIdEnvironmentProvider ?? CreateStrictMock<IOpenIdEnvironmentProvider>().Object,
            effectiveSettingsResolver ?? CreateStrictMock<IEffectiveSettingsResolver>().Object,
            clientCoherenceValidator ?? CreateStrictMock<IClientCoherenceValidator>().Object
        );

    private SeededLocalAccountValidator CreateSeededLocalAccountValidator(
        IEffectiveSettingsResolver? effectiveSettingsResolver = null,
        IPasswordPolicy? passwordPolicy = null
    ) =>
        new(
            effectiveSettingsResolver ?? CreateStrictMock<IEffectiveSettingsResolver>().Object,
            passwordPolicy ?? CreateStrictMock<IPasswordPolicy>().Object
        );

    private SeedTenantCommand CreateCommand(string tenantId, TenantPlane plane) =>
        // Strict mock: the store manager is untouched on the guard paths under test.
        CreateCommand(tenantId, plane, CreateStrictMock<IStoreManager>().Object);

    private static SeedTenantCommand CreateCommand(
        string tenantId,
        TenantPlane plane,
        IStoreManager storeManager
    )
    {
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

    private static DeveloperSpaClientSeed CreateSpaClient() =>
        new() { ClientId = "spa", RedirectUris = ["https://spa.example/callback"] };

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNothingConfigured_DoesNothing()
    {
        // There are no built-in clients and no default password, so the default options seed nothing.
        var handler = CreateHandler(new DeveloperInteractiveSeedOptions());

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNoTenantIsNamed_DoesNothing()
    {
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions
            {
                SpaClients = [CreateSpaClient()],
                AdminPassword = "dev-password",
            }
        );

        await handler.HandleAsync(CreateCommand("root", TenantPlane.Root), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantDoesNotMatchConfiguredTarget_DoesNothing()
    {
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions
            {
                SpaClients = [CreateSpaClient()],
                AdminPassword = "dev-password",
                TenantId = "tenant-a",
            }
        );

        await handler.HandleAsync(
            CreateCommand("tenant-b", TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSpaClientConfigured_RegistersItAsAPublicClient()
    {
        // A single-page application cannot keep a secret, so it registers 'none': its one method, and its type.
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        // The validation result stops the seeding before the client is stored, once the document is captured.
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

        var options = new DeveloperInteractiveSeedOptions
        {
            TenantId = WorkloadTenantId,
            SpaClients = [CreateSpaClient()],
            AdminPassword = null,
        };
        var handler = new DeveloperInteractiveSeedHandler(
            options,
            CreateSeededClientValidator(
                mockSettingSerializer.Object,
                mockEnvironmentProvider.Object
            ),
            CreateSeededLocalAccountValidator()
        );

        var mockClientStore = CreateStrictMock<IClientStore>();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("spa", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
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
            OpenIdProtocol.ClientAuthenticationMethods.None,
            settingsDocument.GetProperty("token_endpoint_auth_method").GetString()
        );
        Assert.False(
            settingsDocument.TryGetProperty("token_endpoint_auth_methods_supported", out _)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheSpaClientWouldBeIncoherent_ThrowsNamingTheClient()
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
                    OpenIdSettingNames.RedirectUris,
                    "https://spa.example/callback",
                    OpenIdSettingNames.RedirectUriKindsSupported
                ),
            ])
            .Verifiable();

        var options = new DeveloperInteractiveSeedOptions
        {
            TenantId = WorkloadTenantId,
            SpaClients = [CreateSpaClient()],
            AdminPassword = null,
        };
        var handler = new DeveloperInteractiveSeedHandler(
            options,
            CreateSeededClientValidator(
                mockSettingSerializer.Object,
                mockEnvironmentProvider.Object,
                mockResolver.Object,
                mockValidator.Object
            ),
            CreateSeededLocalAccountValidator()
        );

        // No AddAsync setup: the strict store refuses a client that would be stored.
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("spa", It.IsAny<CancellationToken>()))
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

        Assert.Contains("'spa'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "https://spa.example/callback",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheAdministratorPasswordIsRefused_ThrowsNamingTheAdministrator()
    {
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

        var mockPolicy = CreateStrictMock<IPasswordPolicy>();
        mockPolicy
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<PasswordPolicyContext>(),
                    It.IsAny<ReadOnlyMemory<char>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                PasswordPolicyResult.Rejected(
                    PasswordPolicyErrors.TooShort,
                    "The password must have at least 15 characters."
                )
            )
            .Verifiable();

        var options = new DeveloperInteractiveSeedOptions
        {
            TenantId = WorkloadTenantId,
            AdminPassword = "short",
        };
        var handler = new DeveloperInteractiveSeedHandler(
            options,
            CreateSeededClientValidator(),
            CreateSeededLocalAccountValidator(mockResolver.Object, mockPolicy.Object)
        );

        // No AddAsync setup: the strict store refuses an account that would be stored.
        var mockAccountStore = CreateStrictMock<ILocalAccountStore>();
        mockAccountStore
            .Setup(x => x.GetByUserNameOrDefaultAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedLocalAccount?)null)
            .Verifiable();
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(mockAccountStore.Object)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'admin'", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
