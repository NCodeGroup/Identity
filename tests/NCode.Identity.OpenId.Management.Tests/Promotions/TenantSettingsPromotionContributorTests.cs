// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Promotions;

public sealed class TenantSettingsPromotionContributorTests : IDisposable
{
    private const string TeamId = "team-1";
    private const string SourceTenantId = "source-tenant";
    private const string TargetTenantId = "target-tenant";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IPromotedSettingsComposer> MockPromotedSettingsComposer { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ISupportTenantId> MockTeamNode { get; }
    private Mock<ISupportTenantId> MockTargetTenantNode { get; }

    public TenantSettingsPromotionContributorTests()
    {
        MockPromotedSettingsComposer = MockRepository.Create<IPromotedSettingsComposer>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockTeamNode = MockRepository.Create<ISupportTenantId>();
        MockTargetTenantNode = MockRepository.Create<ISupportTenantId>();
    }

    public void Dispose() => MockRepository.Verify();

    private TenantSettingsPromotionContributor CreateContributor() =>
        new(MockPromotedSettingsComposer.Object, MockSettingSerializer.Object);

    private static PersistedTenant CreateTenant(string tenantId, string settingsJson) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            TeamId = TeamId,
            SettingsConcurrencyToken = "settings-token",
            Settings = JsonElement.Parse(settingsJson),
        }.Build();

    private TenantPromotionContext CreateContext(
        string sourceSettingsJson = """{"a":1}""",
        string targetSettingsJson = """{"a":0}""",
        IReadOnlyList<TenantPromotionEnvironmentSettings>? environmentSettings = null
    ) =>
        new()
        {
            SourceTenant = CreateTenant(SourceTenantId, sourceSettingsJson),
            TargetTenant = CreateTenant(TargetTenantId, targetSettingsJson),
            TeamNode = MockTeamNode.Object,
            TargetTenantNode = MockTargetTenantNode.Object,
            StoreManager = MockStoreManager.Object,
            AmbientTenantAccessor = MockAmbientTenantAccessor.Object,
            JsonSerializerOptions = new JsonSerializerOptions(),
            EnvironmentSettings = environmentSettings ?? [],
            TargetTenantSettings = JsonElement.Parse(targetSettingsJson),
        };

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

    private void SetupCompose(
        TenantPromotionContext context,
        PromotedSettings promoted,
        JsonElement? environmentSettings = null
    ) =>
        MockPromotedSettingsComposer
            .Setup(x =>
                x.Compose(
                    It.Is<JsonElement>(source =>
                        JsonElement.DeepEquals(source, context.SourceTenant.Settings.Value)
                    ),
                    It.Is<JsonElement?>(target =>
                        target.HasValue
                        && JsonElement.DeepEquals(target.Value, context.TargetTenant.Settings.Value)
                    ),
                    It.Is<JsonElement?>(environment =>
                        environmentSettings.HasValue
                            ? environment.HasValue
                                && JsonElement.DeepEquals(
                                    environment.Value,
                                    environmentSettings.Value
                                )
                            : !environment.HasValue
                    )
                )
            )
            .Returns(promoted)
            .Verifiable();

    private void SetupValidate(
        PromotedSettings promoted,
        TenantPromotionContext context,
        params SettingError[] errors
    ) =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.Is<JsonElement>(document =>
                        JsonElement.DeepEquals(document, promoted.Document)
                    ),
                    OpenIdSettingScopes.Tenant,
                    context.JsonSerializerOptions
                )
            )
            .Returns(errors)
            .Verifiable();

    private void SetupChangedLimits(
        TenantPromotionContext context,
        PromotedSettings promoted,
        params string[] changedLimits
    ) =>
        MockSettingSerializer
            .Setup(x =>
                x.GetChangedLimits(
                    It.Is<JsonElement>(current =>
                        JsonElement.DeepEquals(current, context.TargetTenant.Settings.Value)
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
    public void Order_Always_IsTheTenantSettingsOrder()
    {
        Assert.Equal(TenantPromotionOrders.TenantSettings, CreateContributor().Order);
    }

    #endregion

    #region PlanAsync Tests

    [Fact]
    public async Task PlanAsync_WhenTheDocumentIsTheTargets_RecordsAnUnchangedChange()
    {
        var context = CreateContext();
        var promoted = CreatePromotedSettings("""{"a":0}""");
        SetupCompose(context, promoted);
        SetupValidate(promoted, context);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var change = Assert.Single(context.Changes);
        Assert.Equal(ManagementResourceTypes.Tenant, change.ResourceType);
        Assert.Equal(TargetTenantId, change.ResourceId);
        Assert.Equal(TenantPromotionActions.Unchanged, change.Action);
        Assert.Empty(context.Errors);
        Assert.Empty(context.Notes);
        Assert.Empty(context.ChangedLimits);
        Assert.True(JsonElement.DeepEquals(promoted.Document, context.TargetTenantSettings));
    }

    [Fact]
    public async Task PlanAsync_WhenTheDocumentDiffers_RecordsAnUpdatedChangeAndTheDocument()
    {
        var context = CreateContext();
        var promoted = CreatePromotedSettings("""{"a":1}""");
        SetupCompose(context, promoted);
        SetupValidate(promoted, context);
        SetupChangedLimits(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var change = Assert.Single(context.Changes);
        Assert.Equal(ManagementResourceTypes.Tenant, change.ResourceType);
        Assert.Equal(TargetTenantId, change.ResourceId);
        Assert.Equal(TenantPromotionActions.Updated, change.Action);
        Assert.Empty(context.Errors);
        Assert.True(JsonElement.DeepEquals(promoted.Document, context.TargetTenantSettings));
    }

    [Fact]
    public async Task PlanAsync_WhenALimitChanges_RecordsTheChangedLimitAtTheTeamNode()
    {
        var context = CreateContext();
        var promoted = CreatePromotedSettings("""{"a":1}""");
        SetupCompose(context, promoted);
        SetupValidate(promoted, context);
        SetupChangedLimits(context, promoted, "limit_a", "limit_b");

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var limitChange = Assert.Single(context.ChangedLimits);
        Assert.Same(MockTeamNode.Object, limitChange.AuthorityAbove);
        Assert.Equal(["limit_a", "limit_b"], limitChange.SettingNames);
    }

    [Fact]
    public async Task PlanAsync_WhenOnlyANonLimitChanges_RecordsNoChangedLimit()
    {
        var context = CreateContext();
        var promoted = CreatePromotedSettings("""{"a":1}""");
        SetupCompose(context, promoted);
        SetupValidate(promoted, context);
        SetupChangedLimits(context, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.ChangedLimits);
    }

    [Fact]
    public async Task PlanAsync_WhenTheRequestSuppliesSettings_ComposesWithThem()
    {
        var supplied = JsonElement.Parse("""{"env":"x"}""");
        var context = CreateContext(
            environmentSettings:
            [
                new TenantPromotionEnvironmentSettings
                {
                    ResourceType = ManagementResourceTypes.Tenant,
                    ResourceId = TargetTenantId,
                    Settings = supplied,
                },
            ]
        );
        var promoted = CreatePromotedSettings("""{"a":0}""");
        SetupCompose(context, promoted, supplied);
        SetupValidate(promoted, context);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Unchanged, Assert.Single(context.Changes).Action);
    }

    [Fact]
    public async Task PlanAsync_WhenSettingsAreRefused_AddsAnErrorPerName()
    {
        var context = CreateContext();
        var promoted = CreatePromotedSettings("""{"a":0}""", refusedSettingNames: ["a_name"]);
        SetupCompose(context, promoted);
        SetupValidate(promoted, context);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal(ManagementResourceTypes.Tenant, error.ResourceType);
        Assert.Equal(TargetTenantId, error.ResourceId);
        Assert.Contains("a_name", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheDocumentIsInvalid_AddsAnErrorPerSettingError()
    {
        var context = CreateContext();
        var promoted = CreatePromotedSettings("""{"a":0}""");
        SetupCompose(context, promoted);
        SetupValidate(promoted, context, new SettingError("a", "The value of 'a' is invalid."));

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal(ManagementResourceTypes.Tenant, error.ResourceType);
        Assert.Equal(TargetTenantId, error.ResourceId);
        Assert.Equal("The value of 'a' is invalid.", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenSettingsAreUnset_AddsANotePerName()
    {
        var context = CreateContext();
        var promoted = CreatePromotedSettings(
            """{"a":0}""",
            unsetSettingNames: ["environment_name"]
        );
        SetupCompose(context, promoted);
        SetupValidate(promoted, context);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var note = Assert.Single(context.Notes);
        Assert.Equal(ManagementResourceTypes.Tenant, note.ResourceType);
        Assert.Equal(TargetTenantId, note.ResourceId);
        Assert.Equal(TenantPromotionNoteCodes.EnvironmentSettingUnset, note.Code);
        Assert.Equal("environment_name", note.SettingName);
    }

    #endregion

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_WhenTheDocumentIsUnchanged_WritesNothing()
    {
        var context = CreateContext();

        await CreateContributor().ApplyAsync(context, CancellationToken.None);

        Assert.Empty(context.AuditEvents);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheDocumentChanged_UpdatesTheSettingsAndStagesAnAuditEvent()
    {
        var context = CreateContext();
        context.TargetTenantSettings = JsonElement.Parse("""{"a":1}""");
        var persisted = new PersistedTenantBuilder
        {
            TenantId = TargetTenantId,
            SettingsConcurrencyToken = "settings-token",
            Settings = JsonElement.Parse("""{"a":0}"""),
        }.BuildSettings();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TargetTenantId, CancellationToken.None))
            .ReturnsAsync(persisted)
            .Verifiable();
        MockTenantStore
            .Setup(x =>
                x.UpdateSettingsAsync(
                    It.Is<PersistedTenantSettings>(settings =>
                        ReferenceEquals(settings, persisted)
                        && JsonElement.DeepEquals(settings.Value, JsonElement.Parse("""{"a":1}"""))
                    ),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateContributor().ApplyAsync(context, CancellationToken.None);

        var auditEvent = Assert.IsType<TenantChangedAuditEvent>(Assert.Single(context.AuditEvents));
        Assert.Equal(AuditEventSources.Management, auditEvent.EventSource);
        Assert.Equal(TargetTenantId, auditEvent.TenantId);
        Assert.Equal(TeamId, auditEvent.TeamId);
        Assert.Equal(TargetTenantId, auditEvent.ResourceId);
        Assert.Equal(ResourceChangeTypes.SettingsUpdated, auditEvent.ChangeType);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);

        Assert.NotNull(auditEvent.ResourceValues);
        var resource = auditEvent.ResourceValues.Value.Deserialize<TenantSettingsResource>(
            JsonSerializerOptions.Web
        );
        Assert.NotNull(resource);
        Assert.Equal(TargetTenantId, resource.TenantId);
        Assert.True(JsonElement.DeepEquals(JsonElement.Parse("""{"a":1}"""), resource.Settings));
    }

    [Fact]
    public async Task ApplyAsync_WhenTheSettingsAreMissing_Throws()
    {
        var context = CreateContext();
        context.TargetTenantSettings = JsonElement.Parse("""{"a":1}""");
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TargetTenantId, CancellationToken.None))
            .ReturnsAsync((PersistedTenantSettings?)null)
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateContributor().ApplyAsync(context, CancellationToken.None)
        );
    }

    #endregion
}
