// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.Json;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.ResourceServers;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Promotions;

public sealed class ResourceServerPromotionContributorTests : IDisposable
{
    private const string TeamId = "team-1";
    private const string SourceTenantId = "source-tenant";
    private const string TargetTenantId = "target-tenant";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IPromotedSettingsComposer> MockPromotedSettingsComposer { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IResourceServerStore> MockResourceServerStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ISupportTenantId> MockTeamNode { get; }
    private Mock<ISupportTenantId> MockTargetTenantNode { get; }

    public ResourceServerPromotionContributorTests()
    {
        MockPromotedSettingsComposer = MockRepository.Create<IPromotedSettingsComposer>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockResourceServerStore = MockRepository.Create<IResourceServerStore>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockTeamNode = MockRepository.Create<ISupportTenantId>();
        MockTargetTenantNode = MockRepository.Create<ISupportTenantId>();
    }

    public void Dispose() => MockRepository.Verify();

    private void SetupStores() =>
        MockStoreManager
            .Setup(x => x.GetStore<IResourceServerStore>())
            .Returns(MockResourceServerStore.Object)
            .Verifiable();

    private void SetupScopes()
    {
        SetupScope(SourceTenantId);
        SetupScope(TargetTenantId);
    }

    private void SetupScope(string tenantId)
    {
        var scope = MockRepository.Create<IDisposable>();
        scope.Setup(x => x.Dispose()).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(tenantId))
            .Returns(scope.Object)
            .Verifiable();
    }

    private ResourceServerPromotionContributor CreateContributor() =>
        new(MockPromotedSettingsComposer.Object, MockSettingSerializer.Object);

    private static PersistedTenant CreateTenant(string tenantId) =>
        new PersistedTenantBuilder { TenantId = tenantId, TeamId = TeamId }.Build();

    private TenantPromotionContext CreateContext(
        IReadOnlyList<TenantPromotionEnvironmentSettings>? environmentSettings = null
    ) =>
        new()
        {
            SourceTenant = CreateTenant(SourceTenantId),
            TargetTenant = CreateTenant(TargetTenantId),
            TeamNode = MockTeamNode.Object,
            TargetTenantNode = MockTargetTenantNode.Object,
            StoreManager = MockStoreManager.Object,
            AmbientTenantAccessor = MockAmbientTenantAccessor.Object,
            JsonSerializerOptions = new JsonSerializerOptions(),
            EnvironmentSettings = environmentSettings ?? [],
            TargetTenantSettings = JsonElements.EmptyObject,
        };

    private static PersistedScope CreateScope(
        string value,
        string? description = null,
        bool isSystem = false
    ) =>
        new()
        {
            Value = value,
            Description = description,
            IsSystem = isSystem,
        };

    private static PersistedResourceServer CreateServer(
        string tenantId,
        string resourceServerId,
        string identifier,
        string name = "Name",
        bool isSystem = false,
        bool isDisabled = false,
        string settingsJson = "{}",
        params PersistedScope[] scopes
    ) =>
        new PersistedResourceServerBuilder
        {
            TenantId = tenantId,
            ResourceServerId = resourceServerId,
            Identifier = identifier,
            Name = name,
            IsSystem = isSystem,
            IsDisabled = isDisabled,
            Settings = JsonElement.Parse(settingsJson),
            Scopes = scopes,
        }.Build();

    private static PromotedSettings CreatePromotedSettings(
        string documentJson,
        List<string>? unsetSettingNames = null,
        List<string>? refusedSettingNames = null
    ) =>
        new()
        {
            Document = JsonElement.Parse(documentJson),
            UnsetSettingNames = unsetSettingNames ?? [],
            RefusedSettingNames = refusedSettingNames ?? [],
        };

    private void SetupSourcePages(params PagedResult<PersistedResourceServer>[] pages)
    {
        string? cursor = null;
        foreach (var page in pages)
        {
            var expectedCursor = cursor;
            MockResourceServerStore
                .Setup(x =>
                    x.GetPageAsync(
                        It.Is<PageRequest>(request =>
                            request.Cursor == expectedCursor && request.Limit == 100
                        ),
                        CancellationToken.None
                    )
                )
                .ReturnsAsync(page)
                .Verifiable();
            cursor = page.NextCursor;
        }
    }

    private void SetupSource(params PersistedResourceServer[] sources) =>
        SetupSourcePages(
            new PagedResult<PersistedResourceServer> { Items = sources, NextCursor = null }
        );

    private void SetupTarget(string identifier, PersistedResourceServer? target) =>
        MockResourceServerStore
            .Setup(x => x.GetByIdentifierOrDefaultAsync(identifier, CancellationToken.None))
            .ReturnsAsync(target)
            .Verifiable();

    private void SetupHolder(string resourceServerId, PersistedResourceServer? holder) =>
        MockResourceServerStore
            .Setup(x => x.GetOrDefaultAsync(resourceServerId, CancellationToken.None))
            .ReturnsAsync(holder)
            .Verifiable();

    private void SetupCompose(
        PersistedResourceServer source,
        PersistedResourceServer? target,
        PromotedSettings promoted,
        JsonElement? environmentSettings = null
    ) =>
        MockPromotedSettingsComposer
            .Setup(x =>
                x.Compose(
                    It.Is<JsonElement>(value =>
                        JsonElement.DeepEquals(value, source.Settings.Value)
                    ),
                    It.Is<JsonElement?>(value =>
                        target == null
                            ? !value.HasValue
                            : value.HasValue
                                && JsonElement.DeepEquals(value.Value, target.Settings.Value)
                    ),
                    It.Is<JsonElement?>(value =>
                        environmentSettings.HasValue
                            ? value.HasValue
                                && JsonElement.DeepEquals(value.Value, environmentSettings.Value)
                            : !value.HasValue
                    )
                )
            )
            .Returns(promoted)
            .Verifiable();

    private void SetupValidate(
        TenantPromotionContext context,
        PromotedSettings promoted,
        params SettingError[] errors
    ) =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.Is<JsonElement>(document =>
                        JsonElement.DeepEquals(document, promoted.Document)
                    ),
                    OpenIdSettingScopes.ResourceServer,
                    context.JsonSerializerOptions
                )
            )
            .Returns(errors)
            .Verifiable();

    private void SetupChangedLimits(
        PersistedResourceServer target,
        PromotedSettings promoted,
        params string[] changedLimits
    ) =>
        MockSettingSerializer
            .Setup(x =>
                x.GetChangedLimits(
                    It.Is<JsonElement>(current =>
                        JsonElement.DeepEquals(current, target.Settings.Value)
                    ),
                    It.Is<JsonElement>(replacement =>
                        JsonElement.DeepEquals(replacement, promoted.Document)
                    )
                )
            )
            .Returns(changedLimits)
            .Verifiable();

    #region Order Property Tests

    [Fact]
    public void Order_Always_IsTheResourceServersOrder()
    {
        Assert.Equal(TenantPromotionOrders.ResourceServers, CreateContributor().Order);
    }

    #endregion

    #region PlanAsync Tests

    [Fact]
    public async Task PlanAsync_WhenTheTargetLacksTheIdentifier_RecordsACreatedChangeAndMapsTheSourceId()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://api");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", null);
        SetupHolder("rs-source", null);
        SetupCompose(source, null, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var change = Assert.Single(context.Changes);
        Assert.Equal(ManagementResourceTypes.ResourceServer, change.ResourceType);
        Assert.Equal("rs-source", change.ResourceId);
        Assert.Equal(TenantPromotionActions.Created, change.Action);
        Assert.Empty(context.Errors);
        Assert.Empty(context.ChangedLimits);
        Assert.True(
            context.TryGetTargetIdentifier(
                ManagementResourceTypes.ResourceServer,
                "rs-source",
                out var targetId
            )
        );
        Assert.Equal("rs-source", targetId);
    }

    [Fact]
    public async Task PlanAsync_WhenTheTargetHasTheIdentifier_MapsTheSourceIdToTheTargetId()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://api");
        var target = CreateServer(TargetTenantId, "rs-target", "https://api");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var change = Assert.Single(context.Changes);
        Assert.Equal("rs-target", change.ResourceId);
        Assert.Equal(TenantPromotionActions.Unchanged, change.Action);
        Assert.True(
            context.TryGetTargetIdentifier(
                ManagementResourceTypes.ResourceServer,
                "rs-source",
                out var targetId
            )
        );
        Assert.Equal("rs-target", targetId);
    }

    [Fact]
    public async Task PlanAsync_MapsEachResourceServerIncludingSystemOnes()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var created = CreateServer(
            SourceTenantId,
            "rs-created",
            "https://created",
            settingsJson: """{"a":1}"""
        );
        var updatedSource = CreateServer(
            SourceTenantId,
            "rs-updated-source",
            "https://updated",
            name: "New",
            settingsJson: """{"b":1}"""
        );
        var updatedTarget = CreateServer(
            TargetTenantId,
            "rs-updated-target",
            "https://updated",
            name: "Old",
            settingsJson: """{"b":1}"""
        );
        var system = CreateServer(
            SourceTenantId,
            "rs-system-source",
            "https://system",
            isSystem: true
        );
        var systemTarget = CreateServer(
            TargetTenantId,
            "rs-system-target",
            "https://system",
            isSystem: true
        );
        var createdPromoted = CreatePromotedSettings("""{"a":1}""");
        var updatedPromoted = CreatePromotedSettings("""{"b":1}""");
        SetupSource(created, updatedSource, system);
        SetupTarget("https://created", null);
        SetupHolder("rs-created", null);
        SetupCompose(created, null, createdPromoted);
        SetupValidate(context, createdPromoted);
        SetupTarget("https://updated", updatedTarget);
        SetupCompose(updatedSource, updatedTarget, updatedPromoted);
        SetupValidate(context, updatedPromoted);
        SetupTarget("https://system", systemTarget);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.True(
            context.TryGetTargetIdentifier(
                ManagementResourceTypes.ResourceServer,
                "rs-created",
                out var createdId
            )
        );
        Assert.Equal("rs-created", createdId);
        Assert.True(
            context.TryGetTargetIdentifier(
                ManagementResourceTypes.ResourceServer,
                "rs-updated-source",
                out var updatedId
            )
        );
        Assert.Equal("rs-updated-target", updatedId);
        Assert.True(
            context.TryGetTargetIdentifier(
                ManagementResourceTypes.ResourceServer,
                "rs-system-source",
                out var systemId
            )
        );
        Assert.Equal("rs-system-target", systemId);
    }

    [Fact]
    public async Task PlanAsync_WhenEverythingMatches_RecordsAnUnchangedChange()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(
            SourceTenantId,
            "rs-source",
            "https://api",
            scopes: [CreateScope("read", "Read"), CreateScope("system", "System", isSystem: true)]
        );
        var target = CreateServer(
            TargetTenantId,
            "rs-target",
            "https://api",
            scopes: [CreateScope("READ", "Read")]
        );
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Unchanged, Assert.Single(context.Changes).Action);
        Assert.Empty(context.ChangedLimits);
    }

    [Fact]
    public async Task PlanAsync_WhenTheNameDiffers_RecordsAnUpdatedChange()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://api", name: "New name");
        var target = CreateServer(TargetTenantId, "rs-target", "https://api", name: "Old name");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Updated, Assert.Single(context.Changes).Action);
        Assert.Empty(context.ChangedLimits);
    }

    [Fact]
    public async Task PlanAsync_WhenAScopeIsMissing_RecordsAnUpdatedChange()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(
            SourceTenantId,
            "rs-source",
            "https://api",
            scopes: [CreateScope("read", "Read")]
        );
        var target = CreateServer(TargetTenantId, "rs-target", "https://api");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Updated, Assert.Single(context.Changes).Action);
    }

    [Fact]
    public async Task PlanAsync_WhenAScopeDescriptionDiffers_RecordsAnUpdatedChange()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(
            SourceTenantId,
            "rs-source",
            "https://api",
            scopes: [CreateScope("read", "New")]
        );
        var target = CreateServer(
            TargetTenantId,
            "rs-target",
            "https://api",
            scopes: [CreateScope("read", "Old")]
        );
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Updated, Assert.Single(context.Changes).Action);
    }

    [Fact]
    public async Task PlanAsync_WhenTheDocumentDiffers_RecordsTheChangedLimitAtTheTargetTenantNode()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(
            SourceTenantId,
            "rs-source",
            "https://api",
            settingsJson: """{"a":1}"""
        );
        var target = CreateServer(
            TargetTenantId,
            "rs-target",
            "https://api",
            settingsJson: """{"a":0}"""
        );
        var promoted = CreatePromotedSettings("""{"a":1}""");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);
        SetupChangedLimits(target, promoted, "limit_a");

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Updated, Assert.Single(context.Changes).Action);
        var limitChange = Assert.Single(context.ChangedLimits);
        Assert.Same(MockTargetTenantNode.Object, limitChange.AuthorityAbove);
        Assert.Equal(["limit_a"], limitChange.SettingNames);
    }

    [Fact]
    public async Task PlanAsync_WhenTheDocumentDiffersWithoutALimit_RecordsNoChangedLimit()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(
            SourceTenantId,
            "rs-source",
            "https://api",
            settingsJson: """{"a":1}"""
        );
        var target = CreateServer(
            TargetTenantId,
            "rs-target",
            "https://api",
            settingsJson: """{"a":0}"""
        );
        var promoted = CreatePromotedSettings("""{"a":1}""");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);
        SetupChangedLimits(target, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.ChangedLimits);
    }

    [Fact]
    public async Task PlanAsync_WhenTheSourceIsSystemAndTheTargetHasIt_OnlyMapsTheId()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://system", isSystem: true);
        var target = CreateServer(TargetTenantId, "rs-target", "https://system", isSystem: true);
        SetupSource(source);
        SetupTarget("https://system", target);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Changes);
        Assert.Empty(context.Errors);
        Assert.True(
            context.TryGetTargetIdentifier(
                ManagementResourceTypes.ResourceServer,
                "rs-source",
                out var targetId
            )
        );
        Assert.Equal("rs-target", targetId);
    }

    [Fact]
    public async Task PlanAsync_WhenTheSourceIsSystemAndTheTargetLacksIt_MapsNothing()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://system", isSystem: true);
        SetupSource(source);
        SetupTarget("https://system", null);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Changes);
        Assert.Empty(context.Errors);
        Assert.False(
            context.TryGetTargetIdentifier(
                ManagementResourceTypes.ResourceServer,
                "rs-source",
                out _
            )
        );
    }

    [Fact]
    public async Task PlanAsync_WhenTheTargetIsSystem_AddsAnError()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://api");
        var target = CreateServer(TargetTenantId, "rs-target", "https://api", isSystem: true);
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal(ManagementResourceTypes.ResourceServer, error.ResourceType);
        Assert.Equal("rs-target", error.ResourceId);
        Assert.Contains("system", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheTargetHoldsTheSourceIdUnderAnotherIdentifier_AddsAnError()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://api");
        var holder = CreateServer(TargetTenantId, "rs-source", "https://other");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", null);
        SetupHolder("rs-source", holder);
        SetupCompose(source, null, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal(ManagementResourceTypes.ResourceServer, error.ResourceType);
        Assert.Equal("rs-source", error.ResourceId);
        Assert.Contains("rs-source", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenSettingsAreRefused_AddsAnErrorPerName()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://api");
        var promoted = CreatePromotedSettings("{}", refusedSettingNames: ["a_name"]);
        SetupSource(source);
        SetupTarget("https://api", null);
        SetupHolder("rs-source", null);
        SetupCompose(source, null, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Contains("a_name", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheDocumentIsInvalid_AddsAnErrorPerSettingError()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://api");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", null);
        SetupHolder("rs-source", null);
        SetupCompose(source, null, promoted);
        SetupValidate(context, promoted, new SettingError("a", "The value of 'a' is invalid."));

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal("The value of 'a' is invalid.", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenSettingsAreUnset_AddsANotePerName()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://api");
        var promoted = CreatePromotedSettings("{}", unsetSettingNames: ["jwks_uri"]);
        SetupSource(source);
        SetupTarget("https://api", null);
        SetupHolder("rs-source", null);
        SetupCompose(source, null, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var note = Assert.Single(context.Notes);
        Assert.Equal(ManagementResourceTypes.ResourceServer, note.ResourceType);
        Assert.Equal("rs-source", note.ResourceId);
        Assert.Equal(TenantPromotionNoteCodes.EnvironmentSettingUnset, note.Code);
        Assert.Equal("jwks_uri", note.SettingName);
    }

    [Fact]
    public async Task PlanAsync_WhenTheRequestSuppliesSettings_ComposesWithThemByTheTargetId()
    {
        SetupStores();
        SetupScopes();
        var supplied = JsonElement.Parse("""{"env":"x"}""");
        var context = CreateContext(
            environmentSettings:
            [
                new TenantPromotionEnvironmentSettings
                {
                    ResourceType = ManagementResourceTypes.ResourceServer,
                    ResourceId = "rs-target",
                    Settings = supplied,
                },
            ]
        );
        var source = CreateServer(SourceTenantId, "rs-source", "https://api");
        var target = CreateServer(TargetTenantId, "rs-target", "https://api");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted, supplied);
        SetupValidate(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Unchanged, Assert.Single(context.Changes).Action);
    }

    [Fact]
    public async Task PlanAsync_WhenTheSourceHasSeveralPages_ReadsEveryPage()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var first = CreateServer(
            SourceTenantId,
            "rs-1",
            "https://one",
            settingsJson: """{"a":1}"""
        );
        var second = CreateServer(
            SourceTenantId,
            "rs-2",
            "https://two",
            settingsJson: """{"b":1}"""
        );
        SetupSourcePages(
            new PagedResult<PersistedResourceServer> { Items = [first], NextCursor = "next" },
            new PagedResult<PersistedResourceServer> { Items = [second], NextCursor = null }
        );
        foreach (var source in new[] { first, second })
        {
            SetupTarget(source.Identifier, null);
            SetupHolder(source.ResourceServerId, null);
            var promoted = CreatePromotedSettings(source.Settings.Value.GetRawText());
            SetupCompose(source, null, promoted);
            SetupValidate(context, promoted);
        }

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(["rs-1", "rs-2"], context.Changes.Select(x => x.ResourceId));
    }

    #endregion

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_WhenTheTargetLacksTheResourceServer_AddsItAndStagesACreatedEvent()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(
            SourceTenantId,
            "rs-source",
            "https://api",
            name: "Orders",
            isDisabled: true,
            scopes: [CreateScope("read", "Read"), CreateScope("system", "System", isSystem: true)]
        );
        var promoted = CreatePromotedSettings("""{"a":1}""");
        SetupSource(source);
        SetupTarget("https://api", null);
        SetupHolder("rs-source", null);
        SetupCompose(source, null, promoted);
        SetupValidate(context, promoted);
        PersistedResourceServer? added = null;
        MockResourceServerStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedResourceServer>(), CancellationToken.None))
            .Callback<PersistedResourceServer, CancellationToken>((item, _) => added = item)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateContributor().ApplyAsync(context, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(TargetTenantId, added.TenantId);
        Assert.Equal("rs-source", added.ResourceServerId);
        Assert.Equal("https://api", added.Identifier);
        Assert.Equal("Orders", added.Name);
        Assert.True(added.IsDisabled);
        Assert.False(added.IsSystem);
        Assert.Empty(added.Secrets.Value);
        Assert.Equal(TargetTenantId, added.Settings.TenantId);
        Assert.Equal("rs-source", added.Settings.ResourceServerId);
        Assert.True(JsonElement.DeepEquals(promoted.Document, added.Settings.Value));
        var scope = Assert.Single(added.Scopes);
        Assert.Equal("read", scope.Value);
        Assert.Equal("Read", scope.Description);
        Assert.False(scope.IsSystem);

        var auditEvent = Assert.IsType<ResourceServerChangedAuditEvent>(
            Assert.Single(context.AuditEvents)
        );
        Assert.Equal(AuditEventSources.Management, auditEvent.EventSource);
        Assert.Equal(TargetTenantId, auditEvent.TenantId);
        Assert.Equal("rs-source", auditEvent.ResourceId);
        Assert.Equal(ResourceChangeTypes.Created, auditEvent.ChangeType);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.NotNull(auditEvent.ResourceValues);
        var resource = auditEvent.ResourceValues.Value.Deserialize<ResourceServerResource>(
            JsonSerializerOptions.Web
        );
        Assert.NotNull(resource);
        Assert.Equal("https://api", resource.Identifier);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheTargetDiffers_UpdatesItAndStagesAnUpdatedEvent()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(
            SourceTenantId,
            "rs-source",
            "https://api",
            name: "New name",
            isDisabled: true,
            settingsJson: """{"a":1}""",
            scopes: [CreateScope("read", "New"), CreateScope("write", "Write")]
        );
        var target = CreateServer(
            TargetTenantId,
            "rs-target",
            "https://api",
            name: "Old name",
            settingsJson: """{"a":0}""",
            scopes: [CreateScope("READ", "Old")]
        );
        var promoted = CreatePromotedSettings("""{"a":1}""");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);
        MockResourceServerStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedResourceServer>(item =>
                        ReferenceEquals(item, target) && item.Name == "New name" && item.IsDisabled
                    ),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockResourceServerStore
            .Setup(x =>
                x.UpdateSettingsAsync(
                    It.Is<PersistedResourceServerSettings>(settings =>
                        ReferenceEquals(settings, target.Settings)
                        && JsonElement.DeepEquals(settings.Value, promoted.Document)
                    ),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockResourceServerStore
            .Setup(x =>
                x.AddScopeAsync(
                    "rs-target",
                    It.Is<PersistedScope>(scope =>
                        scope.Value == "write" && scope.Description == "Write" && !scope.IsSystem
                    ),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockResourceServerStore
            .Setup(x =>
                x.UpdateScopeAsync(
                    "rs-target",
                    It.Is<PersistedScope>(scope =>
                        scope.Value == "READ" && scope.Description == "New" && !scope.IsSystem
                    ),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateContributor().ApplyAsync(context, CancellationToken.None);

        var auditEvent = Assert.IsType<ResourceServerChangedAuditEvent>(
            Assert.Single(context.AuditEvents)
        );
        Assert.Equal(TargetTenantId, auditEvent.TenantId);
        Assert.Equal("rs-target", auditEvent.ResourceId);
        Assert.Equal(ResourceChangeTypes.Updated, auditEvent.ChangeType);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheTargetMatches_WritesNothing()
    {
        SetupStores();
        SetupScopes();
        var context = CreateContext();
        var source = CreateServer(SourceTenantId, "rs-source", "https://api");
        var target = CreateServer(TargetTenantId, "rs-target", "https://api");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("https://api", target);
        SetupCompose(source, target, promoted);
        SetupValidate(context, promoted);

        await CreateContributor().ApplyAsync(context, CancellationToken.None);

        Assert.Empty(context.AuditEvents);
    }

    #endregion
}
