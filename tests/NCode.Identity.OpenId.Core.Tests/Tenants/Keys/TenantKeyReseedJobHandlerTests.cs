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
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyReseedJobHandlerTests : BaseTests
{
    private const string FirstTenantId = "acme";
    private const string SecondTenantId = "globex";

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IMediator> MockMediator { get; }
    private IReadOnlySettingCollection PreviousServerSettings { get; }
    private IReadOnlySettingCollection CurrentServerSettings { get; }
    private ServiceProvider ServiceProvider { get; }
    private TenantKeyReseedJobHandler Handler { get; }

    public TenantKeyReseedJobHandlerTests()
    {
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantStore = CreateStrictMock<ITenantStore>();
        MockMediator = CreateStrictMock<IMediator>();
        PreviousServerSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        CurrentServerSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;

        ServiceProvider = new ServiceCollection()
            .AddScoped(_ => MockMediator.Object)
            .BuildServiceProvider();

        Handler = new TenantKeyReseedJobHandler(
            Options.Create(OpenIdOptions),
            MockEffectiveSettingsResolver.Object,
            MockStoreManagerFactory.Object,
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            CreateLooseMock<ILogger<TenantKeyReseedJobHandler>>().Object
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServiceProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenATenantsListsGainAnAlgorithm_AnnouncesItsSettingsChange()
    {
        SetupServerSettings();
        SetupStoreManager();

        // Two pages, so the job follows the cursor rather than stopping after the first page.
        SetupTenantPage(cursor: null, nextCursor: "next", FirstTenantId);
        SetupTenantPage(cursor: "next", nextCursor: null, SecondTenantId);
        SetupTenantSettings(FirstTenantId, previous: [], current: ["RS256"]);
        SetupTenantSettings(SecondTenantId, previous: ["ES256"], current: ["ES256", "RS256"]);
        SetupAnnouncement(FirstTenantId);
        SetupAnnouncement(SecondTenantId);

        await Handler.HandleAsync(CreateJob(), Context(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenATenantAlreadyUsedTheAlgorithm_DoesNotAnnounceIt()
    {
        SetupServerSettings();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId);
        SetupTenantSettings(FirstTenantId, previous: ["RS256"], current: ["RS256"]);

        // Strict mocks: no SendAsync setup, so announcing the tenant would fail the test.
        await Handler.HandleAsync(CreateJob(), Context(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenAnAnnouncementFails_ContinuesWithTheNextTenant()
    {
        SetupServerSettings();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId, SecondTenantId);
        SetupTenantSettings(FirstTenantId, previous: [], current: ["RS256"]);
        SetupTenantSettings(SecondTenantId, previous: [], current: ["RS256"]);
        SetupAnnouncement(
            FirstTenantId,
            ValueTask.FromException(new InvalidOperationException("boom"))
        );
        SetupAnnouncement(SecondTenantId);

        await Handler.HandleAsync(CreateJob(), Context(), CancellationToken.None);
    }

    #endregion

    #region Scaffolding

    private static bool HasVersion(JsonElement element, int version) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("v", out var value)
        && value.GetInt32() == version;

    private static bool IsTenantDocument(JsonElement element, string tenantId) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("tenant", out var value)
        && value.GetString() == tenantId;

    private static DispatchContext Context() =>
        new()
        {
            DispatchId = Guid.NewGuid(),
            Attempt = 1,
            MaxAttempts = 5,
        };

    private static TenantKeyReseedJob CreateJob() =>
        new()
        {
            PreviousServerSettings = JsonElement.Parse("""{"v":1}"""),
            ServerSettings = JsonElement.Parse("""{"v":2}"""),
            Algorithms = ["RS256"],
        };

    private void SetupServerSettings()
    {
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveServerAsync(
                    It.Is<JsonElement>(e => HasVersion(e, 1)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(PreviousServerSettings)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveServerAsync(
                    It.Is<JsonElement>(e => HasVersion(e, 2)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CurrentServerSettings)
            .Verifiable();
    }

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

    private void SetupTenantSettings(string tenantId, string[] previous, string[] current)
    {
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    PreviousServerSettings,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => IsTenantDocument(e, tenantId))
                )
            )
            .Returns(CreateSettings(previous))
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    CurrentServerSettings,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => IsTenantDocument(e, tenantId))
                )
            )
            .Returns(CreateSettings(current))
            .Verifiable();
    }

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

    #endregion
}
