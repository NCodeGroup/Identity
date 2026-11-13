// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Promotions;

public sealed class ClientPromotionContributorTests : IDisposable
{
    private const string TeamId = "team-1";
    private const string SourceTenantId = "source-tenant";
    private const string TargetTenantId = "target-tenant";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IPromotedSettingsComposer> MockPromotedSettingsComposer { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IClientCoherenceValidator> MockClientCoherenceValidator { get; }
    private Mock<ITenantClientCoherenceChecker> MockTenantClientCoherenceChecker { get; }
    private Mock<IClientKeySourceChecker> MockClientKeySourceChecker { get; }
    private Mock<IHostManagedClientChecker> MockHostManagedClientChecker { get; }
    private Mock<IOutboundHostChecker> MockOutboundHostChecker { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ISupportTenantId> MockTeamNode { get; }
    private Mock<ISupportTenantId> MockTargetTenantNode { get; }
    private Mock<IReadOnlySettingCollection> MockTeamSettings { get; }
    private Mock<IReadOnlySettingCollection> MockTenantSettings { get; }
    private HashSet<string> ScopedTenantIds { get; } = [];

    public ClientPromotionContributorTests()
    {
        MockPromotedSettingsComposer = MockRepository.Create<IPromotedSettingsComposer>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockClientCoherenceValidator = MockRepository.Create<IClientCoherenceValidator>();
        MockTenantClientCoherenceChecker = MockRepository.Create<ITenantClientCoherenceChecker>();
        MockClientKeySourceChecker = MockRepository.Create<IClientKeySourceChecker>();
        MockHostManagedClientChecker = MockRepository.Create<IHostManagedClientChecker>();
        MockOutboundHostChecker = MockRepository.Create<IOutboundHostChecker>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockTeamNode = MockRepository.Create<ISupportTenantId>();
        MockTargetTenantNode = MockRepository.Create<ISupportTenantId>();
        MockTeamSettings = MockRepository.Create<IReadOnlySettingCollection>();
        MockTenantSettings = MockRepository.Create<IReadOnlySettingCollection>();
    }

    public void Dispose() => MockRepository.Verify();

    private void SetupStores() =>
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();

    private void SetupSourceScope() => SetupScope(SourceTenantId);

    private void SetupTargetScope() => SetupScope(TargetTenantId);

    private void SetupScope(string tenantId)
    {
        if (!ScopedTenantIds.Add(tenantId))
        {
            return;
        }

        var scope = MockRepository.Create<IDisposable>();
        scope.Setup(x => x.Dispose()).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(tenantId))
            .Returns(scope.Object)
            .Verifiable();
    }

    private void SetupTeamSettings() =>
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync(TeamId, CancellationToken.None))
            .ReturnsAsync(MockTeamSettings.Object)
            .Verifiable();

    private void SetupCompute()
    {
        SetupStores();
        SetupTeamSettings();
        SetupResolveTenant("{}", MockTenantSettings);
        SetupSourceScope();
    }

    private void SetupNoClientHostManaged() =>
        MockHostManagedClientChecker
            .Setup(x => x.IsHostManaged(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false)
            .Verifiable();

    private void SetupNoJwksUri() =>
        MockClientKeySourceChecker
            .Setup(x => x.HasJwksUri(It.IsAny<JsonElement>()))
            .Returns(false)
            .Verifiable();

    private void SetupJwksUriHostCheck(string host, bool isRefused) =>
        MockOutboundHostChecker
            .Setup(x => x.IsRefusedAsync(host, CancellationToken.None))
            .ReturnsAsync(isRefused)
            .Verifiable();

    private ClientPromotionContributor CreateContributor() =>
        new(
            MockPromotedSettingsComposer.Object,
            MockSettingSerializer.Object,
            MockEffectiveSettingsResolver.Object,
            MockClientCoherenceValidator.Object,
            MockTenantClientCoherenceChecker.Object,
            MockClientKeySourceChecker.Object,
            MockHostManagedClientChecker.Object,
            new DefaultClientJwksUriChecker(MockOutboundHostChecker.Object)
        );

    private void SetupResolveTenant(string documentJson, Mock<IReadOnlySettingCollection> result)
    {
        var document = JsonElement.Parse(documentJson);
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    MockTeamSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(value => JsonElement.DeepEquals(value, document))
                )
            )
            .Returns(result.Object)
            .Verifiable();
    }

    private static PersistedTenant CreateTenant(string tenantId) =>
        new PersistedTenantBuilder { TenantId = tenantId, TeamId = TeamId }.Build();

    private TenantPromotionContext CreateContext(
        IReadOnlyList<TenantPromotionEnvironmentSettings>? environmentSettings = null,
        string targetTenantSettingsJson = "{}"
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
            TargetTenantSettings = JsonElement.Parse(targetTenantSettingsJson),
        };

    private static PersistedSecret CreateSecret(string secretType) =>
        new PersistedSecretBuilder
        {
            RetiresWhen = DateTimeOffset.MaxValue,
            ExpiresWhen = DateTimeOffset.MaxValue,
            SecretType = secretType,
            SecretEncoding = "base64",
            Origin = "imported",
            EncodedValue = "value",
        }.Build();

    private static PersistedClient CreateClient(
        string tenantId,
        string clientId,
        string? resourceServerId = null,
        bool isDisabled = false,
        string settingsJson = "{}",
        string principalId = "principal-1",
        string? owningNodeType = null,
        params PersistedSecret[] secrets
    ) =>
        new PersistedClientBuilder
        {
            TenantId = tenantId,
            ClientId = clientId,
            ConcurrencyToken = "token",
            SettingsConcurrencyToken = "settings-token",
            SecretsConcurrencyToken = "secrets-token",
            IsDisabled = isDisabled,
            PrincipalId = principalId,
            ResourceServerId = resourceServerId,
            OwningNodeType = owningNodeType,
            OwningNodeId = owningNodeType is null ? null : "owner-1",
            Settings = JsonElement.Parse(settingsJson),
            Secrets = secrets,
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

    private void SetupSourcePages(params PagedResult<PersistedClient>[] pages)
    {
        string? cursor = null;
        foreach (var page in pages)
        {
            var expectedCursor = cursor;
            MockClientStore
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

    private void SetupSource(params PersistedClient[] sources) =>
        SetupSourcePages(new PagedResult<PersistedClient> { Items = sources, NextCursor = null });

    private void SetupTarget(string clientId, PersistedClient? target) =>
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(clientId, CancellationToken.None))
            .ReturnsAsync(target)
            .Verifiable();

    private void SetupHostManaged(string tenantId, string clientId) =>
        MockHostManagedClientChecker
            .Setup(x => x.IsHostManaged(tenantId, clientId))
            .Returns(true)
            .Verifiable();

    private void SetupCompose(
        PersistedClient source,
        PersistedClient? target,
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
                    OpenIdSettingScopes.Client,
                    context.JsonSerializerOptions
                )
            )
            .Returns(errors)
            .Verifiable();

    private void SetupCoherence(
        Mock<IReadOnlySettingCollection> tenantSettings,
        PromotedSettings promoted,
        params ClientCoherenceViolation[] violations
    ) =>
        MockClientCoherenceValidator
            .Setup(x =>
                x.Validate(
                    tenantSettings.Object,
                    It.Is<JsonElement>(document =>
                        JsonElement.DeepEquals(document, promoted.Document)
                    )
                )
            )
            .Returns(violations)
            .Verifiable();

    private void SetupChangedLimits(
        PersistedClient target,
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

    private PromotedSettings SetupClient(
        TenantPromotionContext context,
        PersistedClient source,
        PersistedClient? target,
        string promotedJson = "{}",
        List<string>? unsetSettingNames = null,
        JsonElement? environmentSettings = null
    )
    {
        var promoted = CreatePromotedSettings(promotedJson, unsetSettingNames);
        SetupTargetScope();
        SetupNoClientHostManaged();
        SetupTarget(source.ClientId, target);
        SetupCompose(source, target, promoted, environmentSettings);
        SetupValidate(context, promoted);
        SetupCoherence(MockTenantSettings, promoted);
        return promoted;
    }

    #region Order Property Tests

    [Fact]
    public void Order_Always_IsTheClientsOrder()
    {
        Assert.Equal(TenantPromotionOrders.Clients, CreateContributor().Order);
    }

    #endregion

    #region PlanAsync Tests

    [Fact]
    public async Task PlanAsync_WhenTheTargetLacksTheClient_RecordsACreatedChangeAndNoAuthority()
    {
        SetupCompute();
        var context = CreateContext();
        var source = CreateClient(
            SourceTenantId,
            "client-1",
            settingsJson: """{"token_endpoint_auth_method":"none"}"""
        );
        SetupSource(source);
        SetupClient(context, source, null, """{"token_endpoint_auth_method":"none"}""");

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var change = Assert.Single(context.Changes);
        Assert.Equal(ManagementResourceTypes.Client, change.ResourceType);
        Assert.Equal("client-1", change.ResourceId);
        Assert.Equal(TenantPromotionActions.Created, change.Action);
        Assert.Empty(context.Errors);
        Assert.Empty(context.Notes);
        Assert.Empty(context.ChangedLimits);
        Assert.Empty(context.RequiredCreateAuthorities);
    }

    [Fact]
    public async Task PlanAsync_WhenEverythingMatches_RecordsAnUnchangedChange()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1");
        var target = CreateClient(TargetTenantId, "client-1");
        SetupSource(source);
        SetupClient(context, source, target);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Unchanged, Assert.Single(context.Changes).Action);
        Assert.Empty(context.ChangedLimits);
        Assert.Empty(context.RequiredCreateAuthorities);
    }

    [Fact]
    public async Task PlanAsync_WhenTheDisabledStateDiffers_RecordsAnUpdatedChange()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1", isDisabled: true);
        var target = CreateClient(TargetTenantId, "client-1");
        SetupSource(source);
        SetupClient(context, source, target);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Updated, Assert.Single(context.Changes).Action);
        Assert.Empty(context.ChangedLimits);
    }

    [Fact]
    public async Task PlanAsync_WhenTheDocumentDiffers_RecordsTheChangedLimitAtTheTargetTenantNode()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1", settingsJson: """{"a":1}""");
        var target = CreateClient(TargetTenantId, "client-1", settingsJson: """{"a":0}""");
        SetupSource(source);
        var promoted = SetupClient(context, source, target, """{"a":1}""");
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
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1", settingsJson: """{"a":1}""");
        var target = CreateClient(TargetTenantId, "client-1", settingsJson: """{"a":0}""");
        SetupSource(source);
        var promoted = SetupClient(context, source, target, """{"a":1}""");
        SetupChangedLimits(target, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.ChangedLimits);
    }

    [Fact]
    public async Task PlanAsync_WhenASourceClientIsHostManaged_NotesItAndReadsNoTarget()
    {
        SetupCompute();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1");
        SetupSource(source);
        SetupHostManaged(SourceTenantId, "client-1");

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Changes);
        var note = Assert.Single(context.Notes);
        Assert.Equal(ManagementResourceTypes.Client, note.ResourceType);
        Assert.Equal("client-1", note.ResourceId);
        Assert.Equal(TenantPromotionNoteCodes.HostManaged, note.Code);
    }

    [Fact]
    public async Task PlanAsync_WhenTheTargetClientIsHostManaged_AddsAnError()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1");
        var target = CreateClient(TargetTenantId, "client-1");
        SetupSource(source);
        SetupClient(context, source, target);
        SetupHostManaged(TargetTenantId, "client-1");

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal(ManagementResourceTypes.Client, error.ResourceType);
        Assert.Equal("client-1", error.ResourceId);
        Assert.Contains("host configures", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheSourceHoldsAMachineClient_LeavesItOut()
    {
        SetupCompute();
        var context = CreateContext();
        var source = CreateClient(
            SourceTenantId,
            "client-1",
            owningNodeType: ResourceNodeTypes.Team
        );
        SetupSource(source);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Changes);
        Assert.Empty(context.Notes);
        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task PlanAsync_WhenTheTargetClientIsAMachineClient_ReportsAnError()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1");
        var target = CreateClient(
            TargetTenantId,
            "client-1",
            owningNodeType: ResourceNodeTypes.Platform
        );
        SetupSource(source);
        SetupClient(context, source, target);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal(ManagementResourceTypes.Client, error.ResourceType);
        Assert.Equal("client-1", error.ResourceId);
        Assert.Contains("machine client", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheDocumentNamesAJwksUriAndTheTargetHoldsPublicKeys_AddsAnError()
    {
        SetupCompute();
        var context = CreateContext();
        var source = CreateClient(
            SourceTenantId,
            "client-1",
            settingsJson: """{"jwks_uri":"https://client/jwks"}"""
        );
        var target = CreateClient(TargetTenantId, "client-1", secrets: CreateSecret("rsa"));
        SetupSource(source);
        var promoted = SetupClient(
            context,
            source,
            target,
            """{"jwks_uri":"https://client/jwks"}"""
        );
        SetupChangedLimits(target, promoted);
        MockClientKeySourceChecker
            .Setup(x =>
                x.HasJwksUri(
                    It.Is<JsonElement>(value => JsonElement.DeepEquals(value, promoted.Document))
                )
            )
            .Returns(true)
            .Verifiable();
        MockClientKeySourceChecker
            .Setup(x => x.HasPublicKeys(target.Secrets.Value))
            .Returns(true)
            .Verifiable();
        SetupJwksUriHostCheck("client", isRefused: false);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Contains("one source", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheJwksUriDiffersFromTheTargetsAndItsHostIsRefused_AddsTheRefusalAsAnError()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(
            SourceTenantId,
            "client-1",
            settingsJson: """{"jwks_uri":"https://client.example/jwks"}"""
        );
        var target = CreateClient(TargetTenantId, "client-1");
        SetupSource(source);
        var promoted = SetupClient(
            context,
            source,
            target,
            """{"jwks_uri":"https://client.example/jwks"}"""
        );
        SetupChangedLimits(target, promoted);
        SetupJwksUriHostCheck("client.example", isRefused: true);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal("client-1", error.ResourceId);
        Assert.Contains("'jwks_uri' host resolves only to addresses", error.Detail);
        Assert.Contains("secrets/import", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheSourceActsForAMappedResourceServer_ReadsTheTargetBoundToIt()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        var source = CreateClient(SourceTenantId, "client-1", resourceServerId: "rs-source");
        var target = CreateClient(TargetTenantId, "client-1", resourceServerId: "rs-target");
        SetupSource(source);
        SetupClient(context, source, target);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Errors);
        Assert.Equal(TenantPromotionActions.Unchanged, Assert.Single(context.Changes).Action);
    }

    [Fact]
    public async Task PlanAsync_WhenTheSourceResourceServerIsNotMapped_AddsAnError()
    {
        SetupCompute();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1", resourceServerId: "rs-source");
        SetupSource(source);
        SetupClient(context, source, null);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Contains("'rs-source'", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheTargetActsForAnotherResourceServer_AddsAnError()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        var source = CreateClient(SourceTenantId, "client-1", resourceServerId: "rs-source");
        var target = CreateClient(TargetTenantId, "client-1", resourceServerId: "rs-other");
        SetupSource(source);
        SetupClient(context, source, target);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Contains("another resource server", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheSourceHasNoResourceServerAndTheTargetHasOne_AddsAnError()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1");
        var target = CreateClient(TargetTenantId, "client-1", resourceServerId: "rs-other");
        SetupSource(source);
        SetupClient(context, source, target);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Single(context.Errors);
    }

    [Fact]
    public async Task PlanAsync_WhenTheDocumentIsInvalid_AddsAnErrorPerSettingError()
    {
        SetupCompute();
        SetupTargetScope();
        SetupNoClientHostManaged();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1");
        var promoted = CreatePromotedSettings("{}", refusedSettingNames: ["redirect"]);
        SetupSource(source);
        SetupTarget("client-1", null);
        SetupCompose(source, null, promoted);
        SetupValidate(context, promoted, new SettingError("a", "The value of 'a' is invalid."));
        SetupCoherence(MockTenantSettings, promoted);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(2, context.Errors.Count);
        Assert.Contains("'redirect'", context.Errors[0].Detail);
        Assert.Equal("The value of 'a' is invalid.", context.Errors[1].Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheDocumentIsIncoherentUnderThePromotedTenant_AddsAnError()
    {
        SetupCompute();
        SetupTargetScope();
        SetupNoClientHostManaged();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("client-1", null);
        SetupCompose(source, null, promoted);
        SetupValidate(context, promoted);
        var violation = new ClientCoherenceViolation("a", "b", "c");
        SetupCoherence(MockTenantSettings, promoted, violation);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(violation.Description, Assert.Single(context.Errors).Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheSourceSetsAnEnvironmentBoundSetting_AddsANote()
    {
        SetupCompute();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1");
        SetupSource(source);
        SetupClient(context, source, null, unsetSettingNames: ["redirect_uris"]);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var note = Assert.Single(context.Notes, x => x.SettingName == "redirect_uris");
        Assert.Equal(TenantPromotionNoteCodes.EnvironmentSettingUnset, note.Code);
        Assert.Equal("client-1", note.ResourceId);
    }

    [Fact]
    public async Task PlanAsync_WhenTheClientNeedsACredentialAndTheTargetHoldsNone_AddsACredentialNote()
    {
        SetupCompute();
        var context = CreateContext();
        var source = CreateClient(
            SourceTenantId,
            "client-1",
            settingsJson: """{"token_endpoint_auth_method":"client_secret_basic"}"""
        );
        SetupSource(source);
        SetupClient(
            context,
            source,
            null,
            """{"token_endpoint_auth_method":"client_secret_basic"}"""
        );

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var note = Assert.Single(context.Notes);
        Assert.Equal(TenantPromotionNoteCodes.CredentialRequired, note.Code);
        Assert.Equal("client-1", note.ResourceId);
    }

    [Fact]
    public async Task PlanAsync_WhenTheTargetHoldsASecret_AddsNoCredentialNote()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(
            SourceTenantId,
            "client-1",
            settingsJson: """{"token_endpoint_auth_method":"client_secret_basic"}"""
        );
        var target = CreateClient(
            TargetTenantId,
            "client-1",
            settingsJson: """{"token_endpoint_auth_method":"client_secret_basic"}""",
            secrets: CreateSecret("oct")
        );
        SetupSource(source);
        SetupClient(
            context,
            source,
            target,
            """{"token_endpoint_auth_method":"client_secret_basic"}"""
        );

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Notes);
    }

    [Fact]
    public async Task PlanAsync_WhenTheTenantSettingsChange_AddsAnErrorForAnIntroducedViolationOfAnUnpromotedClient()
    {
        SetupCompute();
        SetupTargetScope();
        SetupNoClientHostManaged();
        var context = CreateContext(targetTenantSettingsJson: """{"a":1}""");
        var newTenantSettings = MockRepository.Create<IReadOnlySettingCollection>();
        SetupResolveTenant("""{"a":1}""", newTenantSettings);
        var source = CreateClient(SourceTenantId, "client-1");
        var promoted = CreatePromotedSettings("{}");
        SetupSource(source);
        SetupTarget("client-1", null);
        SetupCompose(source, null, promoted);
        SetupValidate(context, promoted);
        SetupCoherence(newTenantSettings, promoted);
        var violation = new ClientCoherenceViolation("a", "b", "c");
        MockTenantClientCoherenceChecker
            .Setup(x =>
                x.FindIntroducedViolationsAsync(
                    MockStoreManager.Object,
                    TargetTenantId,
                    MockTenantSettings.Object,
                    newTenantSettings.Object,
                    CancellationToken.None
                )
            )
            .ReturnsAsync([
                new ClientCoherenceFinding("client-1", violation),
                new ClientCoherenceFinding("client-2", violation),
            ])
            .Verifiable();

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal("client-2", error.ResourceId);
        Assert.Contains("'client-2'", error.Detail);
        Assert.Contains(violation.Description, error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenTheSourceHasSeveralPages_ReadsEveryPage()
    {
        SetupCompute();
        var context = CreateContext();
        var first = CreateClient(SourceTenantId, "client-1", settingsJson: """{"a":1}""");
        var second = CreateClient(SourceTenantId, "client-2", settingsJson: """{"b":1}""");
        SetupSourcePages(
            new PagedResult<PersistedClient> { Items = [first], NextCursor = "next" },
            new PagedResult<PersistedClient> { Items = [second], NextCursor = null }
        );
        SetupClient(context, first, null, """{"a":1}""");
        SetupClient(context, second, null, """{"b":1}""");

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(["client-1", "client-2"], context.Changes.Select(x => x.ResourceId));
    }

    #endregion

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_WhenTheTargetLacksTheClient_AddsItAndStagesACreatedEvent()
    {
        SetupCompute();
        var context = CreateContext();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        var source = CreateClient(
            SourceTenantId,
            "client-1",
            resourceServerId: "rs-source",
            isDisabled: true
        );
        SetupSource(source);
        var promoted = SetupClient(context, source, null, """{"a":1}""");
        PersistedClient? added = null;
        MockClientStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), CancellationToken.None))
            .Callback<PersistedClient, CancellationToken>((item, _) => added = item)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateContributor().ApplyAsync(context, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(TargetTenantId, added.TenantId);
        Assert.Equal("client-1", added.ClientId);
        Assert.True(added.IsDisabled);
        Assert.Equal("rs-target", added.ResourceServerId);
        Assert.Empty(added.Secrets.Value);
        Assert.Equal(TargetTenantId, added.Settings.TenantId);
        Assert.Equal("client-1", added.Settings.ClientId);
        Assert.True(JsonElement.DeepEquals(promoted.Document, added.Settings.Value));

        var auditEvent = Assert.IsType<ClientChangedAuditEvent>(Assert.Single(context.AuditEvents));
        Assert.Equal(AuditEventSources.Management, auditEvent.EventSource);
        Assert.Equal(TargetTenantId, auditEvent.TenantId);
        Assert.Equal("client-1", auditEvent.ResourceId);
        Assert.Equal(ResourceChangeTypes.Created, auditEvent.ChangeType);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.NotNull(auditEvent.ResourceValues);
        var resource = auditEvent.ResourceValues.Value.Deserialize<ClientResource>(
            JsonSerializerOptions.Web
        );
        Assert.NotNull(resource);
        Assert.False(resource.IsHostManaged);
        Assert.Equal("rs-target", resource.ResourceServerId);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheTargetDiffers_UpdatesItAndStagesAnUpdatedEvent()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(
            SourceTenantId,
            "client-1",
            isDisabled: true,
            settingsJson: """{"a":1}"""
        );
        var target = CreateClient(TargetTenantId, "client-1", settingsJson: """{"a":0}""");
        SetupSource(source);
        var promoted = SetupClient(context, source, target, """{"a":1}""");
        MockClientStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedClient>(item =>
                        ReferenceEquals(item, target) && item.IsDisabled
                    ),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockClientStore
            .Setup(x =>
                x.UpdateSettingsAsync(
                    It.Is<PersistedClientSettings>(settings =>
                        ReferenceEquals(settings, target.Settings)
                        && JsonElement.DeepEquals(settings.Value, promoted.Document)
                    ),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateContributor().ApplyAsync(context, CancellationToken.None);

        var auditEvent = Assert.IsType<ClientChangedAuditEvent>(Assert.Single(context.AuditEvents));
        Assert.Equal(TargetTenantId, auditEvent.TenantId);
        Assert.Equal("client-1", auditEvent.ResourceId);
        Assert.Equal(ResourceChangeTypes.Updated, auditEvent.ChangeType);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheTargetMatches_WritesNothing()
    {
        SetupCompute();
        SetupNoJwksUri();
        var context = CreateContext();
        var source = CreateClient(SourceTenantId, "client-1");
        var target = CreateClient(TargetTenantId, "client-1");
        SetupSource(source);
        SetupClient(context, source, target);

        await CreateContributor().ApplyAsync(context, CancellationToken.None);

        Assert.Empty(context.AuditEvents);
    }

    #endregion
}
