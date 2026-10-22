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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class BootstrapAdminSeedHandlerTests : BaseTests
{
    private const string ClientId = "bootstrap-admin";
    private const string ClientSecret = "secret-value";
    private const string WorkloadTenantId = "default";

    private BootstrapAdminSeedHandler CreateHandler(
        BootstrapAdminOptions bootstrapOptions,
        SeededClientValidator? seededClientValidator = null
    )
    {
        // Strict mocks: none of these collaborators are touched on the no-op guard paths.
        var mockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        var mockCryptoService = CreateStrictMock<ICryptoService>();
        var mockLogger = CreateLooseMock<ILogger<BootstrapAdminSeedHandler>>();

        seededClientValidator ??= new SeededClientValidator(
            CreateStrictMock<ISettingSerializer>().Object,
            CreateStrictMock<IOpenIdEnvironmentProvider>().Object,
            CreateStrictMock<IEffectiveSettingsResolver>().Object,
            CreateStrictMock<IClientCoherenceValidator>().Object
        );

        return new BootstrapAdminSeedHandler(
            Options.Create(bootstrapOptions),
            mockSecretGenerator.Object,
            mockCryptoService.Object,
            TimeProvider.System,
            seededClientValidator,
            mockLogger.Object
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

        // No AddAsync setup: the strict store refuses a client that would be stored. The strict secret generator has
        // no setup either, because the secret is imported only after the client is accepted.
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
