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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyReconciliationHandlerTests : BaseTests
{
    private const string ServerId = "default";
    private const string FirstTenantId = "acme";
    private const string SecondTenantId = "globex";

    private static TenantKeySpecification Rs256 { get; } =
        new()
        {
            Algorithm = "RS256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
        };

    private static TenantKeySpecification Es256 { get; } =
        new()
        {
            Algorithm = "ES256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
        };

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<ITenantKeyPolicy> MockPolicy { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IMediator> MockMediator { get; }
    private IReadOnlySettingCollection ServerSettings { get; }
    private IReadOnlySettingCollection PreviousServerSettings { get; }
    private ServiceProvider ServiceProvider { get; }
    private TenantKeyReconciliationHandler Handler { get; }

    public TenantKeyReconciliationHandlerTests()
    {
        MockPolicy = CreateStrictMock<ITenantKeyPolicy>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantStore = CreateStrictMock<ITenantStore>();
        MockMediator = CreateStrictMock<IMediator>();
        ServerSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        PreviousServerSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;

        ServiceProvider = new ServiceCollection()
            .AddScoped(_ => MockMediator.Object)
            .BuildServiceProvider();

        Handler = CreateHandler(CreateLooseMock<ILogger<TenantKeyReconciliationHandler>>().Object);
    }

    private TenantKeyReconciliationHandler CreateHandler(
        ILogger<TenantKeyReconciliationHandler> logger
    ) =>
        new(
            Options.Create(OpenIdOptions),
            MockPolicy.Object,
            MockEffectiveSettingsResolver.Object,
            MockStoreManagerFactory.Object,
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            logger
        );

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServiceProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    private OpenIdTenantKeyProvisioningOptions KeyProvisioning =>
        OpenIdOptions.Tenant.KeyProvisioning;

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenATenantLacksAKeyItsSettingsUse_AnnouncesItsSettingsChange()
    {
        SetupSpecifications();
        SetupStoreManager();

        // Two pages, so the pass follows the cursor rather than stopping after the first page.
        SetupTenantPage(cursor: null, nextCursor: "next", FirstTenantId);
        SetupTenantPage(cursor: "next", nextCursor: null, SecondTenantId);

        // The first tenant lists RS256 and holds only a key that does not satisfy it; the second lists ES256 and holds a
        // key that satisfies it.
        var other = CreateSecret("other");
        var held = CreateSecret("held");
        SetupTenantSettings(FirstTenantId, ["RS256"]);
        SetupSecrets(FirstTenantId, [other]);
        MockPolicy.Setup(x => x.IsSatisfiedBy(Rs256, other)).Returns(false).Verifiable();
        SetupAnnouncement(FirstTenantId);
        SetupTenantSettings(SecondTenantId, ["ES256"]);
        SetupSecrets(SecondTenantId, [held]);
        MockPolicy.Setup(x => x.IsSatisfiedBy(Es256, held)).Returns(true).Verifiable();

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenATenantUsesNoConfiguredAlgorithm_DoesNotReadItsKeys()
    {
        SetupSpecifications();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId);

        // PS256 is listed but not configured, so no key is needed. Strict mocks: no secrets setup, so reading the
        // tenant's keys would fail the test.
        SetupTenantSettings(FirstTenantId, ["PS256"]);

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenAnAnnouncementFails_ContinuesWithTheNextTenant()
    {
        SetupSpecifications();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId, SecondTenantId);

        // Both tenants list RS256 and hold only a key that does not satisfy it.
        var other = CreateSecret("other");
        MockPolicy.Setup(x => x.IsSatisfiedBy(Rs256, other)).Returns(false).Verifiable();
        SetupTenantSettings(FirstTenantId, ["RS256"]);
        SetupSecrets(FirstTenantId, [other]);
        SetupAnnouncement(
            FirstTenantId,
            ValueTask.FromException(new InvalidOperationException("boom"))
        );
        SetupTenantSettings(SecondTenantId, ["RS256"]);
        SetupSecrets(SecondTenantId, [other]);
        SetupAnnouncement(SecondTenantId);

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenATenantHoldsNoKey_LeavesItToItsFirstMaterialization()
    {
        SetupSpecifications();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId, SecondTenantId);

        // Both tenants list RS256 and hold no key, one without any secrets and one with an empty collection. Strict
        // mocks: no announcement setup, so sending one, or testing a key that does not exist, fails the test.
        SetupTenantSettings(FirstTenantId, ["RS256"]);
        SetupSecrets(FirstTenantId, secrets: null);
        SetupTenantSettings(SecondTenantId, ["RS256"]);
        SetupSecrets(SecondTenantId, secrets: []);

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheChangeAddsNoAlgorithmATenantUses_DoesNotReadItsKeys()
    {
        SetupSpecifications();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId);

        // The tenant uses RS256 under the current settings and used RS256 and ES256 under the previous ones, so the
        // change adds nothing it uses. The handler logs a tenant it cannot handle and continues, so a strict logger
        // without setups fails the test when the handler reads the tenant's keys.
        SetupTenantSettings(FirstTenantId, ["RS256"]);
        SetupTenantSettings(PreviousServerSettings, FirstTenantId, ["RS256", "ES256"]);
        var handler = CreateHandler(
            CreateStrictMock<ILogger<TenantKeyReconciliationHandler>>().Object
        );

        await handler.HandleAsync(CreateCommandWithPreviousSettings(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheChangeAddsAnAlgorithmATenantUses_AnnouncesItsSettingsChange()
    {
        SetupSpecifications();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId);

        // The tenant uses RS256 under the current settings and used PS256 under the previous ones, and holds only a key
        // that does not satisfy RS256.
        var other = CreateSecret("other");
        SetupTenantSettings(FirstTenantId, ["RS256"]);
        SetupTenantSettings(PreviousServerSettings, FirstTenantId, ["PS256"]);
        SetupSecrets(FirstTenantId, [other]);
        MockPolicy.Setup(x => x.IsSatisfiedBy(Rs256, other)).Returns(false).Verifiable();
        SetupAnnouncement(FirstTenantId);

        await Handler.HandleAsync(CreateCommandWithPreviousSettings(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenProvisioningIsDisabled_ReadsNoTenant()
    {
        // Every mock is strict with no setup: resolving the specifications or reading a tenant fails the test.
        KeyProvisioning.Enabled = false;

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    #endregion

    #region Scaffolding

    private static bool IsTenantDocument(JsonElement element, string tenantId) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("tenant", out var value)
        && value.GetString() == tenantId;

    private ReconcileServerSettingsCommand CreateCommand() => new(ServerId, ServerSettings);

    private ReconcileServerSettingsCommand CreateCommandWithPreviousSettings() =>
        new(ServerId, ServerSettings) { PreviousSettings = PreviousServerSettings };

    private void SetupSpecifications() =>
        MockPolicy
            .Setup(x => x.ResolveSpecifications(KeyProvisioning))
            .Returns(
                new TenantKeySpecificationResolution
                {
                    Specifications = [Rs256, Es256],
                    Errors = [],
                }
            )
            .Verifiable();

    private void SetupStoreManager()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupTenantPage(string? cursor, string? nextCursor, params string[] tenantIds) =>
        MockTenantStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == cursor && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedTenantSettings>
                {
                    Items =
                    [
                        .. tenantIds.Select(tenantId => new PersistedTenantSettings
                        {
                            TenantId = tenantId,
                            Value = JsonElement.Parse($$"""{"tenant":"{{tenantId}}"}"""),
                        }),
                    ],
                    NextCursor = nextCursor,
                }
            )
            .Verifiable();

    private void SetupTenantSettings(string tenantId, string[] algorithms) =>
        SetupTenantSettings(ServerSettings, tenantId, algorithms);

    private void SetupTenantSettings(
        IReadOnlySettingCollection serverSettings,
        string tenantId,
        string[] algorithms
    ) =>
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    serverSettings,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => IsTenantDocument(e, tenantId))
                )
            )
            .Returns(CreateSettings(algorithms))
            .Verifiable();

    private void SetupSecrets(string tenantId, IReadOnlyCollection<PersistedSecret>? secrets) =>
        MockTenantStore
            .Setup(x => x.GetSecretsOrDefaultAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                secrets is null
                    ? null
                    : new PersistedTenantSecrets { TenantId = tenantId, Value = secrets }
            )
            .Verifiable();

    private void SetupAnnouncement(string tenantId, ValueTask? result = null) =>
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<TenantChangedCommand>(command =>
                        command.TenantId == tenantId && command.SettingsChanged
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(result ?? ValueTask.CompletedTask)
            .Verifiable();

    private IReadOnlySettingCollection CreateSettings(string[] algorithms)
    {
        // The tenant lists its algorithms under one setting only; every other lookup finds nothing.
        object? value = algorithms.ToList();
        var mockSettings = CreateLooseMock<ISettingCollection>();
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingNames.IdTokenSigningAlgValuesSupported, out value)
            )
            .Returns(true);
        return mockSettings.Object;
    }

    private static PersistedSecret CreateSecret(string secretId) =>
        new()
        {
            SecretId = secretId,
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretKeyTypes.Ecc,
            SecretEncoding = SecretEncodings.Pkcs8,
            KeySizeBits = 256,
            EncodedValue = "AA",
        };

    #endregion
}
