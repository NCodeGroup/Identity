// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeySeedHandlerTests : BaseTests
{
    private const string TenantId = "acme";
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static TenantKeySpecification Rs256 { get; } =
        new()
        {
            Algorithm = "RS256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
        };

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<ITenantKeyPolicy> MockPolicy { get; }
    private Mock<ITenantKeyRolloverPlanner> MockPlanner { get; }
    private Mock<ITenantKeyRollover> MockRollover { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IEventPublisher> MockEventPublisher { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private IReadOnlySettingCollection EffectiveSettings { get; }
    private TenantKeySeedHandler Handler { get; }

    public TenantKeySeedHandlerTests()
    {
        MockPolicy = CreateStrictMock<ITenantKeyPolicy>();
        MockPlanner = CreateStrictMock<ITenantKeyRolloverPlanner>();
        MockRollover = CreateStrictMock<ITenantKeyRollover>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockEventPublisher = CreateStrictMock<IEventPublisher>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantStore = CreateStrictMock<ITenantStore>();
        EffectiveSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;

        Handler = new TenantKeySeedHandler(
            Options.Create(OpenIdOptions),
            MockPolicy.Object,
            MockPlanner.Object,
            MockRollover.Object,
            MockEffectiveSettingsResolver.Object,
            MockEventPublisher.Object,
            new FakeTimeProvider(Now)
        );
    }

    private OpenIdTenantKeyProvisioningOptions KeyProvisioning =>
        OpenIdOptions.Tenant.KeyProvisioning;

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenProvisioningDisabled_LeavesTheTenantUntouched()
    {
        KeyProvisioning.Enabled = false;
        var tenant = CreateTenant([]);
        var original = tenant.Secrets;

        await Handler.HandleAsync(CreateCommand(tenant), CancellationToken.None);

        Assert.Same(original, tenant.Secrets);
    }

    [Fact]
    public async Task HandleAsync_WhenThePlanIsEmpty_LeavesTheTenantUntouchedAndQueuesNothing()
    {
        // The rollover is strict with no setup: applying an empty plan fails the test.
        var held = CreateSecret("held");
        var tenant = CreateTenant([held]);
        var original = tenant.Secrets;
        SetupSpecifications(Rs256);
        SetupSettings();
        SetupPlan(tenant, new TenantKeyRolloverPlan { Now = Now });
        var command = CreateCommand(tenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Same(original, tenant.Secrets);
        Assert.Empty(command.Context.PostCommitSteps);
    }

    [Fact]
    public async Task HandleAsync_WhenThePlanChangesKeys_AppliesItAndCarriesTheResultOnTheTenant()
    {
        var held = CreateSecret("held");
        var generated = CreateSecret("generated");
        var tenant = CreateTenant([held]);
        SetupSpecifications(Rs256);
        SetupSettings();
        var plan = SetupGeneratingPlan(tenant);
        SetupApply(tenant, plan, [held, generated], []);

        await Handler.HandleAsync(CreateCommand(tenant), CancellationToken.None);

        // The materialization that follows reads this instance, so it carries the keys as the plan left them.
        Assert.Equal([held, generated], tenant.Secrets.Value);
        Assert.Equal(TenantId, tenant.Secrets.TenantId);
        Assert.Equal("token", tenant.Secrets.ConcurrencyToken);
    }

    [Fact]
    public async Task HandleAsync_WhenThePlanChangesKeys_PublishesTheAuditEventsOnlyAfterTheCommit()
    {
        // The publisher is strict with no setup until the step runs: an event published before the commit fails
        // the test, and a seed that loses its commit never runs the step.
        var held = CreateSecret("held");
        var tenant = CreateTenant([held]);
        SetupSpecifications(Rs256);
        SetupSettings();
        var plan = SetupGeneratingPlan(tenant);
        var auditEvent = CreateAuditEvent("generated");
        SetupApply(tenant, plan, [held], [auditEvent]);
        var command = CreateCommand(tenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        var step = Assert.Single(command.Context.PostCommitSteps);
        MockEventPublisher
            .Setup(x => x.PublishAsync(auditEvent, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);
        await step(CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_PlansFromTheTenantsEffectiveSettings()
    {
        // The tenant's settings materialize only after the seed, so the seed resolves the tenant's persisted document
        // onto its team's committed settings through the resolver, as the materialized tenant will.
        var tenant = CreateTenant([]);
        SetupSpecifications(Rs256);
        SetupSettings();
        TenantKeyRolloverPlan? planned = null;
        MockPlanner
            .Setup(x =>
                x.Plan(
                    It.Is<IReadOnlyList<TenantKeySpecification>>(specifications =>
                        specifications.SequenceEqual(new[] { Rs256 })
                    ),
                    tenant.Secrets.Value,
                    EffectiveSettings,
                    Now
                )
            )
            .Returns(() => planned = new TenantKeyRolloverPlan { Now = Now })
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(tenant), CancellationToken.None);

        Assert.NotNull(planned);
    }

    [Fact]
    public async Task HandleAsync_WhenOptionsCannotBeHonored_Throws()
    {
        MockPolicy
            .Setup(x => x.ResolveSpecifications(KeyProvisioning))
            .Returns(
                new TenantKeySpecificationResolution
                {
                    Specifications = [],
                    Errors = ["The algorithm 'XX999' is not registered."],
                }
            )
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Handler.HandleAsync(CreateCommand(CreateTenant([])), CancellationToken.None)
        );

        Assert.Contains("'XX999'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region MediatorPriority Property Tests

    [Fact]
    public void MediatorPriority_IsLow_SoContributorsThatHealSecretsRunFirst()
    {
        Assert.Equal(DefaultMediatorPriorities.Low, Handler.MediatorPriority);
    }

    #endregion

    #region Helpers

    private void SetupSpecifications(params TenantKeySpecification[] specifications) =>
        MockPolicy
            .Setup(x => x.ResolveSpecifications(KeyProvisioning))
            .Returns(
                new TenantKeySpecificationResolution
                {
                    Specifications = [.. specifications],
                    Errors = [],
                }
            )
            .Verifiable();

    private void SetupSettings()
    {
        var mockTeamSettings = CreateStrictMock<IReadOnlySettingCollection>();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTeamSettings.Object)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    mockTeamSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(EffectiveSettings)
            .Verifiable();
    }

    private void SetupPlan(PersistedTenant tenant, TenantKeyRolloverPlan plan) =>
        MockPlanner
            .Setup(x =>
                x.Plan(
                    It.IsAny<IReadOnlyList<TenantKeySpecification>>(),
                    tenant.Secrets.Value,
                    EffectiveSettings,
                    Now
                )
            )
            .Returns(plan)
            .Verifiable();

    private TenantKeyRolloverPlan SetupGeneratingPlan(PersistedTenant tenant)
    {
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Generations.Add(
            new TenantKeyGeneration
            {
                Specification = Rs256,
                ActivatesWhen = Now,
                RetiresWhen = Now.AddDays(90),
                ExpiresWhen = Now.AddDays(97),
                Reason = TenantKeyChangeReasons.NoActiveKey,
            }
        );
        SetupPlan(tenant, plan);
        return plan;
    }

    private void SetupApply(
        PersistedTenant tenant,
        TenantKeyRolloverPlan plan,
        IReadOnlyCollection<PersistedSecret> secrets,
        IReadOnlyList<TenantKeyAuditEvent> auditEvents
    )
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockRollover
            .Setup(x =>
                x.ApplyAsync(
                    MockTenantStore.Object,
                    TenantId,
                    tenant.Secrets.Value,
                    plan,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new TenantKeyRolloverResult
                {
                    Secrets = secrets,
                    AuditEvents = auditEvents,
                    RemovedSecrets = [],
                }
            )
            .Verifiable();
    }

    private static TenantKeyAuditEvent CreateAuditEvent(string secretId) =>
        new()
        {
            EventId = Guid.CreateVersion7(Now),
            EventTime = Now,
            EventSource = AuditEventSources.OpenId,
            TenantId = TenantId,
            Actor = AuditActor.System,
            Outcome = AuditOutcome.Success,
            Change = TenantKeyChanges.Generated,
            Reason = TenantKeyChangeReasons.NoActiveKey,
            SecretId = secretId,
            KeyId = secretId,
            SecretType = SecretKeyTypes.Rsa,
            Algorithm = "RS256",
            Use = SecretKeyUses.Signature,
            ActivatesWhen = Now,
            RetiresWhen = Now.AddDays(90),
            ExpiresWhen = Now.AddDays(97),
        };

    private SeedTenantCommand CreateCommand(PersistedTenant tenant) =>
        new(
            new TenantSeedContext
            {
                Tenant = tenant,
                Plane = TenantPlane.Workload,
                StoreManager = MockStoreManager.Object,
            }
        );

    private static PersistedTenant CreateTenant(IReadOnlyCollection<PersistedSecret> secrets) =>
        new PersistedTenantBuilder
        {
            TenantId = TenantId,
            Issuer = "https://op.example/acme",
            ConcurrencyToken = "token",
            Settings = default,
            SecretsConcurrencyToken = "token",
            Secrets = secrets,
        }.Build();

    private static PersistedSecret CreateSecret(string secretId) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            SecretType = SecretKeyTypes.Rsa,
            SecretEncoding = SecretEncodings.Pkcs8,
            KeySizeBits = 3072,
        }.Build();

    #endregion
}
