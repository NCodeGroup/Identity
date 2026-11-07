// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public sealed class DefaultClientCoherenceReporterTests : BaseTests
{
    private const string TenantId = "acme";
    private const string FirstClientId = "web";
    private const string SecondClientId = "api";

    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<IClientCoherenceValidator> MockValidator { get; }
    private IReadOnlySettingCollection TenantSettings { get; }
    private DefaultClientCoherenceReporter Reporter { get; }

    public DefaultClientCoherenceReporterTests()
    {
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockClientStore = CreateStrictMock<IClientStore>();
        MockAmbientTenantAccessor = CreateStrictMock<IAmbientTenantAccessor>();
        MockValidator = CreateStrictMock<IClientCoherenceValidator>();
        TenantSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;

        Reporter = new DefaultClientCoherenceReporter(
            MockAmbientTenantAccessor.Object,
            MockValidator.Object
        );
    }

    #region FindIncoherentClientsAsync Tests

    [Fact]
    public async Task FindIncoherentClientsAsync_WhenAClientBreaksARule_ReturnsItsViolations()
    {
        var page = new PageRequest { Limit = 100 };
        var violation = new ClientCoherenceViolation(
            "redirect_uris",
            "http://x",
            "redirect_uri_kinds_supported"
        );
        SetupClientStore();
        SetupScope([]);
        SetupClientPage(page, null, [], FirstClientId, SecondClientId);
        SetupValidation(FirstClientId, [violation]);
        SetupValidation(SecondClientId, []);

        var result = await Reporter.FindIncoherentClientsAsync(
            MockStoreManager.Object,
            TenantId,
            TenantSettings,
            page,
            CancellationToken.None
        );

        var incoherentClient = Assert.Single(result.Items);
        Assert.Equal(FirstClientId, incoherentClient.ClientId);
        Assert.Equal([violation], incoherentClient.Violations);
        Assert.Null(incoherentClient.DocumentError);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task FindIncoherentClientsAsync_WhenADocumentCannotBeHonored_ReturnsTheReason()
    {
        const string reason = "the document cannot be honored";
        var page = new PageRequest { Limit = 100 };
        SetupClientStore();
        SetupScope([]);
        SetupClientPage(page, null, [], FirstClientId);
        MockValidator
            .Setup(x =>
                x.Validate(TenantSettings, It.Is<JsonElement>(e => IsDocument(e, FirstClientId)))
            )
            .Throws(new JsonException(reason))
            .Verifiable();

        var result = await Reporter.FindIncoherentClientsAsync(
            MockStoreManager.Object,
            TenantId,
            TenantSettings,
            page,
            CancellationToken.None
        );

        var incoherentClient = Assert.Single(result.Items);
        Assert.Equal(FirstClientId, incoherentClient.ClientId);
        Assert.Equal(reason, incoherentClient.DocumentError);
        Assert.Empty(incoherentClient.Violations);
    }

    [Fact]
    public async Task FindIncoherentClientsAsync_WhenEveryClientIsCoherent_ReturnsTheNextCursor()
    {
        var page = new PageRequest { Limit = 100 };
        SetupClientStore();
        SetupScope([]);
        SetupClientPage(page, "next", [], SecondClientId);
        SetupValidation(SecondClientId, []);

        var result = await Reporter.FindIncoherentClientsAsync(
            MockStoreManager.Object,
            TenantId,
            TenantSettings,
            page,
            CancellationToken.None
        );

        Assert.Empty(result.Items);
        Assert.Equal("next", result.NextCursor);
    }

    [Fact]
    public async Task FindIncoherentClientsAsync_ReadsThePageUnderTheTenantsScope()
    {
        var page = new PageRequest { Cursor = "c1", Limit = 10 };
        List<string> events = [];
        SetupClientStore();
        SetupScope(events);
        SetupClientPage(page, null, events, FirstClientId);
        SetupValidation(FirstClientId, []);

        await Reporter.FindIncoherentClientsAsync(
            MockStoreManager.Object,
            TenantId,
            TenantSettings,
            page,
            CancellationToken.None
        );

        Assert.Equal(["begin", "read", "end"], events);
    }

    #endregion

    #region Scaffolding

    private static bool IsDocument(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("name", out var value)
        && value.GetString() == name;

    private static JsonElement CreateDocument(string name) =>
        JsonElement.Parse($$"""{"name":"{{name}}"}""");

    private static PersistedClientSettings CreateClientSettings(string clientId) =>
        new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = clientId,
            Settings = CreateDocument(clientId),
        }.BuildSettings();

    private void SetupClientStore() =>
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();

    // Records, in the events, when the tenant's scope begins and ends.
    private void SetupScope(List<string> events)
    {
        var mockScope = CreateStrictMock<IDisposable>();
        mockScope.Setup(x => x.Dispose()).Callback(() => events.Add("end")).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Callback(() => events.Add("begin"))
            .Returns(mockScope.Object)
            .Verifiable();
    }

    // Expects the caller's page request and records, in the events, when the page is read.
    private void SetupClientPage(
        PageRequest page,
        string? nextCursor,
        List<string> events,
        params string[] clientIds
    ) =>
        MockClientStore
            .Setup(x => x.GetSettingsPageAsync(page, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                events.Add("read");
                return ValueTask.FromResult(
                    new PagedResult<PersistedClientSettings>
                    {
                        Items = [.. clientIds.Select(CreateClientSettings)],
                        NextCursor = nextCursor,
                    }
                );
            })
            .Verifiable();

    private void SetupValidation(
        string clientId,
        IReadOnlyList<ClientCoherenceViolation> violations
    ) =>
        MockValidator
            .Setup(x =>
                x.Validate(TenantSettings, It.Is<JsonElement>(e => IsDocument(e, clientId)))
            )
            .Returns(violations)
            .Verifiable();

    #endregion
}
