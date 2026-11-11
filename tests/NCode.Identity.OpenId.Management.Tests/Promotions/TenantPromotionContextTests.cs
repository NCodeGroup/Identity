// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.Json;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Promotions;

public sealed class TenantPromotionContextTests : IDisposable
{
    private const string SourceTenantId = "source-tenant";
    private const string TargetTenantId = "target-tenant";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ISupportTenantId> MockTeamNode { get; }
    private Mock<ISupportTenantId> MockTargetTenantNode { get; }

    public TenantPromotionContextTests()
    {
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockTeamNode = MockRepository.Create<ISupportTenantId>();
        MockTargetTenantNode = MockRepository.Create<ISupportTenantId>();
    }

    public void Dispose() => MockRepository.Verify();

    private static PersistedTenant CreateTenant(string tenantId) =>
        new PersistedTenantBuilder { TenantId = tenantId, TeamId = "team-1" }.Build();

    private TenantPromotionContext CreateContext(
        IReadOnlyList<TenantPromotionEnvironmentSettings>? environmentSettings = null
    ) =>
        new()
        {
            SourceTenant = CreateTenant(SourceTenantId),
            TargetTenant = CreateTenant(TargetTenantId),
            TeamNode = MockTeamNode.Object,
            TargetTenantNode = MockTargetTenantNode.Object,
            StoreManager = MockRepository.Create<IStoreManager>().Object,
            AmbientTenantAccessor = MockAmbientTenantAccessor.Object,
            JsonSerializerOptions = new JsonSerializerOptions(),
            EnvironmentSettings = environmentSettings ?? [],
            TargetTenantSettings = JsonElements.EmptyObject,
        };

    private static TenantPromotionEnvironmentSettings CreateEnvironmentSettings(
        string resourceType,
        string resourceId,
        string json
    ) =>
        new()
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            Settings = JsonElement.Parse(json),
        };

    #region TargetTenantSettings Property Tests

    [Fact]
    public void TargetTenantSettings_Set_ReplacesTheDocument()
    {
        var context = CreateContext();
        var settings = JsonElement.Parse("""{"a":1}""");

        context.TargetTenantSettings = settings;

        Assert.Equal("""{"a":1}""", context.TargetTenantSettings.GetRawText());
    }

    #endregion

    #region BeginSourceScope Tests

    [Fact]
    public void BeginSourceScope_OpensTheScopeOfTheSourceTenant()
    {
        var scope = MockRepository.Create<IDisposable>();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(SourceTenantId))
            .Returns(scope.Object)
            .Verifiable();
        var context = CreateContext();

        var actual = context.BeginSourceScope();

        Assert.Same(scope.Object, actual);
    }

    #endregion

    #region BeginTargetScope Tests

    [Fact]
    public void BeginTargetScope_OpensTheScopeOfTheTargetTenant()
    {
        var scope = MockRepository.Create<IDisposable>();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TargetTenantId))
            .Returns(scope.Object)
            .Verifiable();
        var context = CreateContext();

        var actual = context.BeginTargetScope();

        Assert.Same(scope.Object, actual);
    }

    #endregion

    #region GetEnvironmentSettingsOrDefault Tests

    [Fact]
    public void GetEnvironmentSettingsOrDefault_NoEntries_ReturnsNull()
    {
        var context = CreateContext();

        var actual = context.GetEnvironmentSettingsOrDefault("client", "c1");

        Assert.Null(actual);
    }

    [Fact]
    public void GetEnvironmentSettingsOrDefault_MatchingEntry_ReturnsItsSettings()
    {
        var context = CreateContext([
            CreateEnvironmentSettings("client", "c2", """{"x":2}"""),
            CreateEnvironmentSettings("client", "c1", """{"x":1}"""),
        ]);

        var actual = context.GetEnvironmentSettingsOrDefault("client", "c1");

        Assert.Equal("""{"x":1}""", actual?.GetRawText());
    }

    [Fact]
    public void GetEnvironmentSettingsOrDefault_RepeatedEntry_ReturnsTheFirst()
    {
        var context = CreateContext([
            CreateEnvironmentSettings("client", "c1", """{"x":1}"""),
            CreateEnvironmentSettings("client", "c1", """{"x":2}"""),
        ]);

        var actual = context.GetEnvironmentSettingsOrDefault("client", "c1");

        Assert.Equal("""{"x":1}""", actual?.GetRawText());
    }

    [Fact]
    public void GetEnvironmentSettingsOrDefault_DifferentCase_ReturnsNull()
    {
        var context = CreateContext([CreateEnvironmentSettings("client", "c1", """{"x":1}""")]);

        var actual = context.GetEnvironmentSettingsOrDefault("client", "C1");

        Assert.Null(actual);
    }

    [Fact]
    public void GetEnvironmentSettingsOrDefault_DifferentResourceType_ReturnsNull()
    {
        var context = CreateContext([CreateEnvironmentSettings("client", "c1", """{"x":1}""")]);

        var actual = context.GetEnvironmentSettingsOrDefault("resource_server", "c1");

        Assert.Null(actual);
    }

    #endregion

    #region AddChange Tests

    [Fact]
    public void AddChange_Null_Throws()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() => context.AddChange(null!));
    }

    [Fact]
    public void AddChange_Change_IsRecordedInOrder()
    {
        var context = CreateContext();
        var first = new TenantPromotionChangeResource
        {
            ResourceType = "client",
            ResourceId = "c1",
            Action = TenantPromotionActions.Created,
        };
        var second = new TenantPromotionChangeResource
        {
            ResourceType = "client",
            ResourceId = "c2",
            Action = TenantPromotionActions.Updated,
        };

        context.AddChange(first);
        context.AddChange(second);

        Assert.Equal([first, second], context.Changes);
    }

    #endregion

    #region AddNote Tests

    [Fact]
    public void AddNote_Null_Throws()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() => context.AddNote(null!));
    }

    [Fact]
    public void AddNote_Note_IsRecordedInOrder()
    {
        var context = CreateContext();
        var first = new TenantPromotionNoteResource
        {
            ResourceType = "client",
            ResourceId = "c1",
            Code = TenantPromotionNoteCodes.CredentialRequired,
            Detail = "first",
        };
        var second = new TenantPromotionNoteResource
        {
            ResourceType = "client",
            ResourceId = "c2",
            Code = TenantPromotionNoteCodes.HostManaged,
            Detail = "second",
        };

        context.AddNote(first);
        context.AddNote(second);

        Assert.Equal([first, second], context.Notes);
    }

    #endregion

    #region AddError Tests

    [Fact]
    public void AddError_Null_Throws()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() => context.AddError(null!));
    }

    [Fact]
    public void AddError_Error_IsRecordedInOrder()
    {
        var context = CreateContext();
        var first = new TenantPromotionErrorResource { ResourceType = "client", Detail = "first" };
        var second = new TenantPromotionErrorResource
        {
            ResourceType = "resource_server",
            Detail = "second",
            ResourceId = "rs1",
        };

        context.AddError(first);
        context.AddError(second);

        Assert.Equal([first, second], context.Errors);
    }

    #endregion

    #region AddAuditEvent Tests

    [Fact]
    public void AddAuditEvent_Null_Throws()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() => context.AddAuditEvent(null!));
    }

    [Fact]
    public void AddAuditEvent_Event_IsRecordedInOrder()
    {
        var context = CreateContext();
        var first = new ClientChangedAuditEvent
        {
            EventSource = AuditEventSources.Management,
            Outcome = AuditOutcome.Success,
            ChangeType = "created",
            ResourceId = "c1",
        };
        var second = new ClientChangedAuditEvent
        {
            EventSource = AuditEventSources.Management,
            Outcome = AuditOutcome.Success,
            ChangeType = "updated",
            ResourceId = "c2",
        };

        context.AddAuditEvent(first);
        context.AddAuditEvent(second);

        Assert.Equal([first, second], context.AuditEvents);
    }

    #endregion

    #region AddChangedLimits Tests

    [Fact]
    public void AddChangedLimits_NullAuthority_Throws()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() =>
            context.AddChangedLimits(null!, ["max_clients"])
        );
    }

    [Fact]
    public void AddChangedLimits_NullNames_Throws()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() =>
            context.AddChangedLimits(MockTeamNode.Object, null!)
        );
    }

    [Fact]
    public void AddChangedLimits_EmptyNames_RecordsNothing()
    {
        var context = CreateContext();

        context.AddChangedLimits(MockTeamNode.Object, []);

        Assert.Empty(context.ChangedLimits);
    }

    [Fact]
    public void AddChangedLimits_Names_RecordsTheNodeAndTheNames()
    {
        var context = CreateContext();

        context.AddChangedLimits(MockTeamNode.Object, ["max_clients", "max_scopes"]);
        context.AddChangedLimits(MockTargetTenantNode.Object, ["max_secrets"]);

        Assert.Equal(2, context.ChangedLimits.Count);
        Assert.Same(MockTeamNode.Object, context.ChangedLimits[0].AuthorityAbove);
        Assert.Equal(["max_clients", "max_scopes"], context.ChangedLimits[0].SettingNames);
        Assert.Same(MockTargetTenantNode.Object, context.ChangedLimits[1].AuthorityAbove);
        Assert.Equal(["max_secrets"], context.ChangedLimits[1].SettingNames);
    }

    [Fact]
    public void AddChangedLimits_Names_CopiesTheNames()
    {
        var context = CreateContext();
        var names = new List<string> { "max_clients" };

        context.AddChangedLimits(MockTeamNode.Object, names);
        names.Add("max_scopes");

        Assert.Equal(["max_clients"], context.ChangedLimits[0].SettingNames);
    }

    #endregion

    #region AddRequiredCreateAuthority Tests

    [Fact]
    public void AddRequiredCreateAuthority_Null_Throws()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() => context.AddRequiredCreateAuthority(null!));
    }

    [Fact]
    public void AddRequiredCreateAuthority_Node_IsRecordedInOrder()
    {
        var context = CreateContext();

        context.AddRequiredCreateAuthority(MockTargetTenantNode.Object);
        context.AddRequiredCreateAuthority(MockTeamNode.Object);

        Assert.Equal(
            [MockTargetTenantNode.Object, MockTeamNode.Object],
            context.RequiredCreateAuthorities
        );
    }

    #endregion

    #region MapIdentifier Tests

    [Fact]
    public void MapIdentifier_NullArguments_Throw()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() => context.MapIdentifier(null!, "s", "t"));
        Assert.Throws<ArgumentNullException>(() =>
            context.MapIdentifier("resource_server", null!, "t")
        );
        Assert.Throws<ArgumentNullException>(() =>
            context.MapIdentifier("resource_server", "s", null!)
        );
    }

    [Fact]
    public void MapIdentifier_Repeated_ReplacesTheTarget()
    {
        var context = CreateContext();

        context.MapIdentifier("resource_server", "s1", "t1");
        context.MapIdentifier("resource_server", "s1", "t2");

        Assert.True(context.TryGetTargetIdentifier("resource_server", "s1", out var targetId));
        Assert.Equal("t2", targetId);
    }

    #endregion

    #region TryGetTargetIdentifier Tests

    [Fact]
    public void TryGetTargetIdentifier_NullArguments_Throw()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() =>
            context.TryGetTargetIdentifier(null!, "s", out _)
        );
        Assert.Throws<ArgumentNullException>(() =>
            context.TryGetTargetIdentifier("resource_server", null!, out _)
        );
    }

    [Fact]
    public void TryGetTargetIdentifier_Mapped_ReturnsTheTargetIdentifier()
    {
        var context = CreateContext();
        context.MapIdentifier("resource_server", "s1", "t1");

        var found = context.TryGetTargetIdentifier("resource_server", "s1", out var targetId);

        Assert.True(found);
        Assert.Equal("t1", targetId);
    }

    [Fact]
    public void TryGetTargetIdentifier_NotMapped_ReturnsFalse()
    {
        var context = CreateContext();

        var found = context.TryGetTargetIdentifier("resource_server", "s1", out var targetId);

        Assert.False(found);
        Assert.Null(targetId);
    }

    [Fact]
    public void TryGetTargetIdentifier_OtherResourceType_ReturnsFalse()
    {
        var context = CreateContext();
        context.MapIdentifier("resource_server", "s1", "t1");

        var found = context.TryGetTargetIdentifier("client", "s1", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetTargetIdentifier_DifferentCase_ReturnsFalse()
    {
        var context = CreateContext();
        context.MapIdentifier("resource_server", "s1", "t1");

        var found = context.TryGetTargetIdentifier("resource_server", "S1", out _);

        Assert.False(found);
    }

    #endregion
}
