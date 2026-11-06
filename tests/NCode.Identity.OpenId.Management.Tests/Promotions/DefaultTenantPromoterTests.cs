// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Json;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Promotions;

public sealed class DefaultTenantPromoterTests : IDisposable
{
    private const string TeamId = "team-1";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ISupportTenantId> MockTeamNode { get; }
    private Mock<ISupportTenantId> MockTargetTenantNode { get; }
    private List<string> Calls { get; } = [];

    public DefaultTenantPromoterTests()
    {
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockTeamNode = MockRepository.Create<ISupportTenantId>();
        MockTargetTenantNode = MockRepository.Create<ISupportTenantId>();
    }

    public void Dispose() => MockRepository.Verify();

    private DefaultTenantPromoter CreatePromoter(
        params ITenantPromotionContributor[] contributors
    ) => new(contributors, MockManagementAuditRecorder.Object);

    private Mock<ITenantPromotionContributor> CreateContributor(int order)
    {
        var mock = MockRepository.Create<ITenantPromotionContributor>();
        mock.Setup(x => x.Order).Returns(order).Verifiable();
        return mock;
    }

    private static PersistedTenant CreateTenant(string tenantId) =>
        new()
        {
            TenantId = tenantId,
            TeamId = TeamId,
            Issuer = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                Value = JsonElements.EmptyObject,
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = "secrets-token",
                Value = [],
            },
        };

    private TenantPromotionContext CreateContext(
        IReadOnlyList<TenantPromotionEnvironmentSettings>? environmentSettings = null
    ) =>
        new()
        {
            SourceTenant = CreateTenant("source-tenant"),
            TargetTenant = CreateTenant("target-tenant"),
            TeamNode = MockTeamNode.Object,
            TargetTenantNode = MockTargetTenantNode.Object,
            StoreManager = MockStoreManager.Object,
            AmbientTenantAccessor = MockAmbientTenantAccessor.Object,
            JsonSerializerOptions = new JsonSerializerOptions(),
            EnvironmentSettings = environmentSettings ?? [],
            TargetTenantSettings = JsonElements.EmptyObject,
        };

    private static TenantPromotionEnvironmentSettings CreateEnvironmentSettings(
        string resourceType,
        string resourceId
    ) =>
        new()
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            Settings = JsonElements.EmptyObject,
        };

    private static TenantChangedAuditEvent CreateAuditEvent(string resourceId) =>
        new()
        {
            EventId = Guid.CreateVersion7(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.Management,
            Outcome = AuditOutcome.Success,
            TeamId = TeamId,
            ResourceId = resourceId,
            ChangeType = ResourceChangeTypes.SettingsUpdated,
        };

    private static TenantPromotionChangeResource CreateChange(
        string resourceType,
        string resourceId
    ) =>
        new()
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            Action = TenantPromotionActions.Updated,
        };

    #region PlanAsync Tests

    [Fact]
    public async Task PlanAsync_WithContributors_PlansEachInAscendingOrder()
    {
        var context = CreateContext();
        var first = CreateContributor(1);
        var second = CreateContributor(2);
        var third = CreateContributor(3);
        foreach (
            var (mock, name) in new[] { (first, "first"), (second, "second"), (third, "third") }
        )
        {
            mock.Setup(x => x.PlanAsync(context, CancellationToken.None))
                .Callback(() => Calls.Add(name))
                .Returns(ValueTask.CompletedTask)
                .Verifiable();
        }

        await CreatePromoter(third.Object, first.Object, second.Object)
            .PlanAsync(context, CancellationToken.None);

        Assert.Equal(["first", "second", "third"], Calls);
    }

    [Fact]
    public async Task PlanAsync_WhenAnEntryNamesARecordedChange_AddsNoError()
    {
        var context = CreateContext([CreateEnvironmentSettings("client", "client-1")]);
        var contributor = CreateContributor(1);
        contributor
            .Setup(x => x.PlanAsync(context, CancellationToken.None))
            .Callback(() => context.AddChange(CreateChange("client", "client-1")))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreatePromoter(contributor.Object).PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task PlanAsync_WhenAnEntryIsListedMoreThanOnce_AddsAnError()
    {
        var context = CreateContext([
            CreateEnvironmentSettings("client", "client-1"),
            CreateEnvironmentSettings("client", "client-1"),
        ]);
        var contributor = CreateContributor(1);
        contributor
            .Setup(x => x.PlanAsync(context, CancellationToken.None))
            .Callback(() => context.AddChange(CreateChange("client", "client-1")))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreatePromoter(contributor.Object).PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal("client", error.ResourceType);
        Assert.Equal("client-1", error.ResourceId);
        Assert.Contains("listed more than once", error.Detail);
    }

    [Fact]
    public async Task PlanAsync_WhenAnEntryNamesNoRecordedChange_AddsAnError()
    {
        var context = CreateContext([CreateEnvironmentSettings("client", "client-2")]);
        var contributor = CreateContributor(1);
        contributor
            .Setup(x => x.PlanAsync(context, CancellationToken.None))
            .Callback(() => context.AddChange(CreateChange("client", "client-1")))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreatePromoter(contributor.Object).PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal("client", error.ResourceType);
        Assert.Equal("client-2", error.ResourceId);
        Assert.Contains("names a resource the promotion does not write", error.Detail);
    }

    #endregion

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_WhenThePlanHasErrors_ThrowsAndAppliesNothing()
    {
        var context = CreateContext();
        context.AddError(
            new TenantPromotionErrorResource
            {
                ResourceType = "client",
                Detail = "A planned error.",
            }
        );
        var contributor = CreateContributor(1);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreatePromoter(contributor.Object)
                .ApplyAsync(new DefaultHttpContext(), context, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ApplyAsync_WithContributors_AppliesSavesAndRecordsEachInAscendingOrder()
    {
        var httpContext = new DefaultHttpContext();
        var context = CreateContext();
        var firstEvent = CreateAuditEvent("first-1");
        var secondEvent = CreateAuditEvent("second-1");
        var thirdEvent = CreateAuditEvent("second-2");

        var first = CreateContributor(1);
        first
            .Setup(x => x.ApplyAsync(context, CancellationToken.None))
            .Callback(() =>
            {
                Calls.Add("apply first");
                context.AddAuditEvent(firstEvent);
            })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var second = CreateContributor(2);
        second
            .Setup(x => x.ApplyAsync(context, CancellationToken.None))
            .Callback(() =>
            {
                Calls.Add("apply second");
                context.AddAuditEvent(secondEvent);
                context.AddAuditEvent(thirdEvent);
            })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var third = CreateContributor(3);
        third
            .Setup(x => x.ApplyAsync(context, CancellationToken.None))
            .Callback(() => Calls.Add("apply third"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        MockStoreManager
            .Setup(x => x.SaveChangesAsync(CancellationToken.None))
            .Callback(() => Calls.Add("save"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        foreach (var auditEvent in new[] { firstEvent, secondEvent, thirdEvent })
        {
            var resourceId = auditEvent.ResourceId;
            MockManagementAuditRecorder
                .Setup(x =>
                    x.RecordResourceChangeAsync(httpContext, auditEvent, CancellationToken.None)
                )
                .Callback(() => Calls.Add($"record {resourceId}"))
                .Returns(ValueTask.CompletedTask)
                .Verifiable();
        }

        await CreatePromoter(third.Object, second.Object, first.Object)
            .ApplyAsync(httpContext, context, CancellationToken.None);

        Assert.Equal(
            [
                "apply first",
                "save",
                "record first-1",
                "apply second",
                "save",
                "record second-1",
                "record second-2",
                "apply third",
                "save",
            ],
            Calls
        );
    }

    #endregion
}
