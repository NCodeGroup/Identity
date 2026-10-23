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
using NCode.Identity.OpenId.Persistence.Tenants;
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
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IClientCoherenceValidator> MockValidator { get; }
    private IReadOnlySettingCollection ServerSettings { get; }

    public ClientCoherenceReportHandlerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantStore = CreateStrictMock<ITenantStore>();
        MockClientStore = CreateStrictMock<IClientStore>();
        MockAmbientTenantAccessor = CreateStrictMock<IAmbientTenantAccessor>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockValidator = CreateStrictMock<IClientCoherenceValidator>();
        ServerSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
    }

    private ClientCoherenceReportHandler CreateHandler(
        ILogger<ClientCoherenceReportHandler> logger
    ) =>
        new(
            MockStoreManagerFactory.Object,
            MockAmbientTenantAccessor.Object,
            MockEffectiveSettingsResolver.Object,
            MockValidator.Object,
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
        SetupScope(FirstTenantId, []);
        SetupClientPage(ClientId);
        SetupValidation(tenantSettings, ClientId, [violation]);
        var handler = CreateHandler(mockLogger.Object);

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_ReadsEachTenantsClientsUnderItsScope()
    {
        var firstSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var secondSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        List<string> events = [];
        SetupStores();
        SetupTenantPage(FirstTenantId, SecondTenantId);
        SetupTenantSettings(FirstTenantId, firstSettings);
        SetupTenantSettings(SecondTenantId, secondSettings);
        SetupScope(FirstTenantId, events);
        SetupScope(SecondTenantId, events);
        SetupClientPage(events, ClientId);
        SetupValidation(firstSettings, ClientId, []);
        SetupValidation(secondSettings, ClientId, []);
        var handler = CreateHandler(
            CreateLooseMock<ILogger<ClientCoherenceReportHandler>>().Object
        );

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(
            [
                $"begin:{FirstTenantId}",
                $"read:{FirstTenantId}",
                $"end:{FirstTenantId}",
                $"begin:{SecondTenantId}",
                $"read:{SecondTenantId}",
                $"end:{SecondTenantId}",
            ],
            events
        );
    }

    [Fact]
    public async Task HandleAsync_WhenATenantCannotBeChecked_ContinuesWithTheNextTenant()
    {
        var secondSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupStores();
        SetupTenantPage(FirstTenantId, SecondTenantId);

        // The first tenant's settings document cannot be resolved, so the tenant is skipped before its scope begins.
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
        SetupScope(SecondTenantId, []);
        SetupClientPage(ClientId);
        SetupValidation(secondSettings, ClientId, []);
        var handler = CreateHandler(
            CreateLooseMock<ILogger<ClientCoherenceReportHandler>>().Object
        );

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenAClientDocumentCannotBeHonored_ReportsIt()
    {
        var tenantSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var mockLogger = CreateWarningLogger(message =>
            message.Contains($"'{ClientId}'") && message.Contains("the document cannot be honored")
        );
        SetupStores();
        SetupTenantPage(FirstTenantId);
        SetupTenantSettings(FirstTenantId, tenantSettings);
        SetupScope(FirstTenantId, []);
        SetupClientPage(ClientId);
        MockValidator
            .Setup(x =>
                x.Validate(tenantSettings, It.Is<JsonElement>(e => IsDocument(e, ClientId)))
            )
            .Throws(new JsonException("the document cannot be honored"))
            .Verifiable();
        var handler = CreateHandler(mockLogger.Object);

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);
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
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
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
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    ServerSettings,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => IsDocument(e, tenantId))
                )
            )
            .Returns(settings)
            .Verifiable();

    // Records the order in which a tenant's scope begins and ends, and its clients are read, in the events.
    private void SetupScope(string tenantId, List<string> events)
    {
        var mockScope = CreateStrictMock<IDisposable>();
        mockScope
            .Setup(x => x.Dispose())
            .Callback(() => events.Add($"end:{tenantId}"))
            .Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(tenantId))
            .Callback(() => events.Add($"begin:{tenantId}"))
            .Returns(mockScope.Object)
            .Verifiable();
    }

    private void SetupClientPage(params string[] clientIds) => SetupClientPage([], clientIds);

    // Every tenant is read through the same store, so the page names the tenant that the ambient scope is for.
    private void SetupClientPage(List<string> events, params string[] clientIds) =>
        MockClientStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == null && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                var tenantId = ReadTenantId(events);
                events.Add($"read:{tenantId}");
                return ValueTask.FromResult(
                    new PagedResult<PersistedClientSettings>
                    {
                        Items =
                        [
                            .. clientIds.Select(clientId => new PersistedClientSettings
                            {
                                TenantId = tenantId,
                                ClientId = clientId,
                                Value = CreateDocument(clientId),
                            }),
                        ],
                        NextCursor = null,
                    }
                );
            })
            .Verifiable();

    // The tenant whose scope has begun and not yet ended, which is the last "begin" event.
    private static string ReadTenantId(List<string> events)
    {
        var begin = events.LastOrDefault(e => e.StartsWith("begin:", StringComparison.Ordinal));
        return begin is null ? FirstTenantId : begin["begin:".Length..];
    }

    private void SetupValidation(
        IReadOnlySettingCollection tenantSettings,
        string clientId,
        IReadOnlyList<ClientCoherenceViolation> violations
    ) =>
        MockValidator
            .Setup(x =>
                x.Validate(tenantSettings, It.Is<JsonElement>(e => IsDocument(e, clientId)))
            )
            .Returns(violations)
            .Verifiable();

    #endregion
}
