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
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public sealed class ClientCoherenceReportHandlerTests : BaseTests
{
    private const string ServerId = "default";
    private const string FirstTenantId = "acme";
    private const string SecondTenantId = "globex";
    private const string ClientId = "web";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IClientCoherenceReporter> MockReporter { get; }
    private Mock<IClientCoherenceValidator> MockValidator { get; }
    private IReadOnlySettingCollection ServerSettings { get; }
    private IReadOnlySettingCollection PreviousServerSettings { get; }

    public ClientCoherenceReportHandlerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantStore = CreateStrictMock<ITenantStore>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockReporter = CreateStrictMock<IClientCoherenceReporter>();
        MockValidator = CreateStrictMock<IClientCoherenceValidator>();
        ServerSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        PreviousServerSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
    }

    private ClientCoherenceReportHandler CreateHandler(
        ILogger<ClientCoherenceReportHandler> logger
    ) =>
        new(
            MockStoreManagerFactory.Object,
            MockEffectiveSettingsResolver.Object,
            MockValidator.Object,
            MockReporter.Object,
            logger
        );

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAClientIsIncoherent_ReportsItsViolations()
    {
        var violation = new ClientCoherenceViolation(
            "redirect_uris",
            "http://x",
            "redirect_uri_kinds_supported"
        );
        var tenantSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var mockLogger = CreateWarningLogger(message =>
            message.Contains($"'{ClientId}'")
            && message.Contains($"'{FirstTenantId}'")
            && message.Contains(violation.Description)
        );
        SetupStores();
        SetupTenantPage(FirstTenantId);
        SetupTenantSettings(FirstTenantId, tenantSettings);
        SetupReport(
            FirstTenantId,
            tenantSettings,
            null,
            null,
            new IncoherentClient { ClientId = ClientId, Violations = [violation] }
        );
        var handler = CreateHandler(mockLogger.Object);

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_ChecksTheClientsOfEveryTenant()
    {
        var firstSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var secondSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupStores();
        SetupTenantPage(FirstTenantId, SecondTenantId);
        SetupTenantSettings(FirstTenantId, firstSettings);
        SetupTenantSettings(SecondTenantId, secondSettings);
        SetupReport(FirstTenantId, firstSettings, null, null);
        SetupReport(SecondTenantId, secondSettings, null, null);
        var handler = CreateHandler(
            CreateLooseMock<ILogger<ClientCoherenceReportHandler>>().Object
        );

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenATenantCannotBeChecked_ContinuesWithTheNextTenant()
    {
        var secondSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupStores();
        SetupTenantPage(FirstTenantId, SecondTenantId);

        // The first tenant's settings document cannot be resolved, so the tenant is skipped before its clients are read.
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    ServerSettings,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => IsDocument(e, FirstTenantId))
                )
            )
            .Throws(new JsonException("boom"))
            .Verifiable();
        SetupTenantSettings(SecondTenantId, secondSettings);
        SetupReport(SecondTenantId, secondSettings, null, null);
        var handler = CreateHandler(
            CreateLooseMock<ILogger<ClientCoherenceReportHandler>>().Object
        );

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenAClientDocumentCannotBeHonored_ReportsTheReason()
    {
        var tenantSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var mockLogger = CreateWarningLogger(message =>
            message.Contains($"'{ClientId}'") && message.Contains("the document cannot be honored")
        );
        SetupStores();
        SetupTenantPage(FirstTenantId);
        SetupTenantSettings(FirstTenantId, tenantSettings);
        SetupReport(
            FirstTenantId,
            tenantSettings,
            null,
            null,
            new IncoherentClient
            {
                ClientId = ClientId,
                Violations = [],
                DocumentError = "the document cannot be honored",
            }
        );
        var handler = CreateHandler(mockLogger.Object);

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_FollowsTheReportersCursor()
    {
        var tenantSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupStores();
        SetupTenantPage(FirstTenantId);
        SetupTenantSettings(FirstTenantId, tenantSettings);
        SetupReport(FirstTenantId, tenantSettings, null, "next");
        SetupReport(FirstTenantId, tenantSettings, "next", null);
        var handler = CreateHandler(
            CreateLooseMock<ILogger<ClientCoherenceReportHandler>>().Object
        );

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheChangeAltersNoSettingARuleReads_DoesNotCheckTheTenantsClients()
    {
        var currentSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var previousSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupStores();
        SetupTenantPage(FirstTenantId);
        SetupTenantSettings(FirstTenantId, currentSettings);
        SetupTenantSettings(PreviousServerSettings, FirstTenantId, previousSettings);
        SetupReadsChangedSetting(previousSettings, currentSettings, changed: false);

        // The handler logs a tenant it cannot check and continues, so the quiet logger fails the test when the handler
        // calls the strict reporter.
        var handler = CreateHandler(CreateQuietLogger().Object);

        await handler.HandleAsync(CreateCommandWithPreviousSettings(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheChangeAltersASettingARuleReads_ReportsTheTenantsIncoherentClients()
    {
        var currentSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var previousSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var mockLogger = CreateWarningLogger(message =>
            message.Contains($"'{ClientId}'") && message.Contains($"'{FirstTenantId}'")
        );
        SetupStores();
        SetupTenantPage(FirstTenantId);
        SetupTenantSettings(FirstTenantId, currentSettings);
        SetupTenantSettings(PreviousServerSettings, FirstTenantId, previousSettings);
        SetupReadsChangedSetting(previousSettings, currentSettings, changed: true);
        SetupReport(
            FirstTenantId,
            currentSettings,
            null,
            null,
            new IncoherentClient
            {
                ClientId = ClientId,
                Violations =
                [
                    new ClientCoherenceViolation(
                        "redirect_uris",
                        "http://x",
                        "redirect_uri_kinds_supported"
                    ),
                ],
            }
        );
        var handler = CreateHandler(mockLogger.Object);

        await handler.HandleAsync(CreateCommandWithPreviousSettings(), CancellationToken.None);
    }

    #endregion

    #region Scaffolding

    private static bool IsDocument(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("name", out var value)
        && value.GetString() == name;

    private static JsonElement CreateDocument(string name) =>
        JsonElement.Parse($$"""{"name":"{{name}}"}""");

    private ReconcileServerSettingsCommand CreateCommand() => new(ServerId, ServerSettings);

    private ReconcileServerSettingsCommand CreateCommandWithPreviousSettings() =>
        new(ServerId, ServerSettings) { PreviousSettings = PreviousServerSettings };

    private void SetupReadsChangedSetting(
        IReadOnlySettingCollection previousSettings,
        IReadOnlySettingCollection currentSettings,
        bool changed
    ) =>
        MockValidator
            .Setup(x => x.ReadsChangedSetting(previousSettings, currentSettings))
            .Returns(changed)
            .Verifiable();

    // A strict logger that accepts only a warning whose message satisfies the predicate.
    private Mock<ILogger<ClientCoherenceReportHandler>> CreateWarningLogger(
        Func<string, bool> isExpectedMessage
    )
    {
        var mockLogger = CreateStrictMock<ILogger<ClientCoherenceReportHandler>>();
        mockLogger
            .Setup(x => x.IsEnabled(It.IsAny<LogLevel>()))
            .Returns((LogLevel level) => level == LogLevel.Warning)
            .Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => IsMessage(state, isExpectedMessage)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();
        return mockLogger;
    }

    private static bool IsMessage(object? state, Func<string, bool> isExpectedMessage) =>
        isExpectedMessage(state?.ToString() ?? string.Empty);

    // A strict logger that skips the information summary and fails the test on any warning, such as the one the handler
    // logs for a tenant it could not check.
    private Mock<ILogger<ClientCoherenceReportHandler>> CreateQuietLogger()
    {
        var mockLogger = CreateStrictMock<ILogger<ClientCoherenceReportHandler>>();
        mockLogger
            .Setup(x => x.IsEnabled(It.IsAny<LogLevel>()))
            .Returns((LogLevel level) => level == LogLevel.Warning)
            .Verifiable();
        return mockLogger;
    }

    private void SetupStores()
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

    private void SetupTenantPage(params string[] tenantIds) =>
        MockTenantStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == null && page.Limit == 100),
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
                            Value = CreateDocument(tenantId),
                        }),
                    ],
                    NextCursor = null,
                }
            )
            .Verifiable();

    private void SetupTenantSettings(string tenantId, IReadOnlySettingCollection settings) =>
        SetupTenantSettings(ServerSettings, tenantId, settings);

    private void SetupTenantSettings(
        IReadOnlySettingCollection serverSettings,
        string tenantId,
        IReadOnlySettingCollection settings
    ) =>
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    serverSettings,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => IsDocument(e, tenantId))
                )
            )
            .Returns(settings)
            .Verifiable();

    // The reporter checks one page of the tenant's clients and returns the incoherent ones among them.
    private void SetupReport(
        string tenantId,
        IReadOnlySettingCollection tenantSettings,
        string? cursor,
        string? nextCursor,
        params IncoherentClient[] clients
    ) =>
        MockReporter
            .Setup(x =>
                x.FindIncoherentClientsAsync(
                    MockStoreManager.Object,
                    tenantId,
                    tenantSettings,
                    It.Is<PageRequest>(page => page.Cursor == cursor && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<IncoherentClient> { Items = clients, NextCursor = nextCursor }
            )
            .Verifiable();

    #endregion
}
