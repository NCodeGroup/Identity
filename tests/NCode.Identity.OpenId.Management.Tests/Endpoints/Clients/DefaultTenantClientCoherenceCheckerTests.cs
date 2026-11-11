// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public class DefaultTenantClientCoherenceCheckerTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private MockRepository MockRepository { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<IClientCoherenceValidator> MockClientCoherenceValidator { get; }
    private Mock<ILogger<DefaultTenantClientCoherenceChecker>> MockLogger { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IReadOnlySettingCollection> MockCurrentSettings { get; }
    private Mock<IReadOnlySettingCollection> MockNewSettings { get; }
    private DefaultTenantClientCoherenceChecker Checker { get; }

    public DefaultTenantClientCoherenceCheckerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockClientCoherenceValidator = MockRepository.Create<IClientCoherenceValidator>();
        MockLogger = MockRepository.Create<ILogger<DefaultTenantClientCoherenceChecker>>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();
        MockCurrentSettings = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        MockNewSettings = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);

        Checker = new DefaultTenantClientCoherenceChecker(
            MockAmbientTenantAccessor.Object,
            MockClientCoherenceValidator.Object,
            MockLogger.Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private Mock<IDisposable> SetupClients(params PersistedClientSettings[] clients)
    {
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();

        var mockScope = MockRepository.Create<IDisposable>();
        mockScope.Setup(x => x.Dispose()).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(mockScope.Object)
            .Verifiable();

        // Two pages, so the checker follows the cursor rather than stopping after the first page.
        MockClientStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == null),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedClientSettings> { Items = [], NextCursor = "next" }
            )
            .Verifiable();
        MockClientStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == "next"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedClientSettings> { Items = clients, NextCursor = null }
            )
            .Verifiable();

        return mockScope;
    }

    private void SetupReadsChangedSetting(bool changed) =>
        MockClientCoherenceValidator
            .Setup(x => x.ReadsChangedSetting(MockCurrentSettings.Object, MockNewSettings.Object))
            .Returns(changed)
            .Verifiable();

    private void SetupValidate(
        Mock<IReadOnlySettingCollection> mockSettings,
        params ClientCoherenceViolation[] violations
    ) =>
        MockClientCoherenceValidator
            .Setup(x => x.Validate(mockSettings.Object, It.IsAny<JsonElement>()))
            .Returns(violations)
            .Verifiable();

    private static ClientCoherenceViolation CreateViolation() =>
        new(
            OpenIdSettingNames.IdTokenSignedResponseAlg,
            "RS256",
            OpenIdSettingNames.IdTokenSigningAlgValuesSupported
        );

    private static ClientCoherenceViolation CreateLoweredLifetimeViolation(string effectiveValue) =>
        new(
            OpenIdSettingNames.AccessTokenLifetime,
            "01:00:00",
            OpenIdSettingNames.AccessTokenLifetime
        )
        {
            Description =
                $"The 'access_token_lifetime' value '01:00:00' exceeds the effective 'access_token_lifetime' value '{effectiveValue}'.",
        };

    private static PersistedClientSettings CreateClientSettings(string clientId) =>
        new PersistedClientBuilder { TenantId = TenantId, ClientId = clientId }.BuildSettings();

    private static PersistedClientSettings CreateUnreadableClientSettings(string clientId) =>
        new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = clientId,
            Settings = JsonSerializer.SerializeToElement(new { retired = true }),
        }.BuildSettings();

    private static bool HasProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _);

    private void SetupUnreadableClientWarning()
    {
        MockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        MockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(eventId => eventId.Id == EventIds.ClientSettingsUnreadable),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();
    }

    #endregion

    #region FindIntroducedViolationsAsync Tests

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenTheChangeIntroducesAViolation_ReturnsTheFinding()
    {
        var violation = CreateViolation();
        SetupReadsChangedSetting(true);
        SetupClients(CreateClientSettings("client-1"));
        SetupValidate(MockNewSettings, violation);
        SetupValidate(MockCurrentSettings);

        var findings = await Checker.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TenantId,
            MockCurrentSettings.Object,
            MockNewSettings.Object,
            CancellationToken.None
        );

        var finding = Assert.Single(findings);
        Assert.Equal("client-1", finding.ClientId);
        Assert.Equal(violation, finding.Violation);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenTheViolationPredatesTheChange_ReturnsEmpty()
    {
        var violation = CreateViolation();
        SetupReadsChangedSetting(true);
        SetupClients(CreateClientSettings("client-1"));
        SetupValidate(MockNewSettings, violation);
        SetupValidate(MockCurrentSettings, violation);

        var findings = await Checker.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TenantId,
            MockCurrentSettings.Object,
            MockNewSettings.Object,
            CancellationToken.None
        );

        Assert.Empty(findings);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenAPredatingViolationNamesAnotherEffectiveValue_ReturnsEmpty()
    {
        SetupReadsChangedSetting(true);
        SetupClients(CreateClientSettings("client-1"));
        SetupValidate(MockNewSettings, CreateLoweredLifetimeViolation("00:04:00"));
        SetupValidate(MockCurrentSettings, CreateLoweredLifetimeViolation("00:03:00"));

        var findings = await Checker.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TenantId,
            MockCurrentSettings.Object,
            MockNewSettings.Object,
            CancellationToken.None
        );

        Assert.Empty(findings);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenTheNewSettingsAreCoherent_ReturnsEmpty()
    {
        SetupReadsChangedSetting(true);
        SetupClients(CreateClientSettings("client-1"));
        SetupValidate(MockNewSettings);

        var findings = await Checker.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TenantId,
            MockCurrentSettings.Object,
            MockNewSettings.Object,
            CancellationToken.None
        );

        Assert.Empty(findings);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenNoSettingARuleReadsChanged_ReadsNoClient()
    {
        SetupReadsChangedSetting(false);

        var findings = await Checker.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TenantId,
            MockCurrentSettings.Object,
            MockNewSettings.Object,
            CancellationToken.None
        );

        Assert.Empty(findings);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenAClientDocumentCannotBeHonored_LogsItAndChecksTheOthers()
    {
        SetupReadsChangedSetting(true);
        SetupClients(CreateUnreadableClientSettings("client-0"), CreateClientSettings("client-1"));
        SetupValidate(MockNewSettings, CreateViolation());
        SetupValidate(MockCurrentSettings);
        MockClientCoherenceValidator
            .Setup(x =>
                x.Validate(
                    MockNewSettings.Object,
                    It.Is<JsonElement>(e => HasProperty(e, "retired"))
                )
            )
            .Throws(
                new JsonException(
                    "The settings are invalid: 'retired' is not a registered setting."
                )
            )
            .Verifiable();
        SetupUnreadableClientWarning();

        var findings = await Checker.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TenantId,
            MockCurrentSettings.Object,
            MockNewSettings.Object,
            CancellationToken.None
        );

        var finding = Assert.Single(findings);
        Assert.Equal("client-1", finding.ClientId);
    }

    #endregion
}
