// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class DefaultTenantKeyRolloverPlannerTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(90);
    private static readonly TimeSpan ActivationDelay = TimeSpan.FromDays(2);
    private static readonly TimeSpan RetirementGrace = TimeSpan.FromDays(7);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<ITenantKeyPolicy> MockPolicy { get; }
    private Mock<IReadOnlySettingCollection> MockSettings { get; }
    private DefaultTenantKeyRolloverPlanner Planner { get; }

    public DefaultTenantKeyRolloverPlannerTests()
    {
        OpenIdOptions.Tenant.KeyRollover.Interval = Interval;
        MockPolicy = CreateStrictMock<ITenantKeyPolicy>();
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        Planner = new DefaultTenantKeyRolloverPlanner(
            Options.Create(OpenIdOptions),
            MockPolicy.Object
        );
        SetupLifetimes(RetirementGrace);
    }

    private static TenantKeySpecification Rs256 => TenantKeyTestData.Rs256;
    private static TenantKeySpecification Es256 => TenantKeyTestData.Es256;

    #region Plan Tests

    [Fact]
    public void Plan_WhenNoKeyServesAUsedAlgorithm_GeneratesAKeyThatActivatesAtOnce()
    {
        SetupUsed("RS256");

        var plan = Planner.Plan([Rs256], [], MockSettings.Object, Now);

        var generation = Assert.Single(plan.Generations);
        Assert.Equal(Rs256, generation.Specification);
        Assert.Equal(Now, generation.ActivatesWhen);
        Assert.Equal(Now + Lifetime, generation.RetiresWhen);
        Assert.Equal(Now + Lifetime + RetirementGrace, generation.ExpiresWhen);
        Assert.Equal(TenantKeyChangeReasons.NoActiveKey, generation.Reason);
        Assert.Empty(plan.Activations);
        Assert.Empty(plan.Retirements);
        Assert.Empty(plan.Removals);
    }

    [Fact]
    public void Plan_WhenTheActiveKeyRetiresFarAhead_ChangesNothing()
    {
        SetupUsed("RS256");
        var active = CreateKey(
            "active",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60)
        );
        SetupSatisfied(Rs256, active);

        var plan = Planner.Plan([Rs256], [active], MockSettings.Object, Now);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Plan_WhenTheActiveKeyRetiresWithinTheDelayAndTheInterval_GeneratesItsSuccessorForTheHandOver()
    {
        SetupUsed("RS256");
        var handOver = Now + ActivationDelay + Interval;
        var active = CreateKey("active", activatesWhen: Now.AddDays(-88), retiresWhen: handOver);
        SetupSatisfied(Rs256, active);

        var plan = Planner.Plan([Rs256], [active], MockSettings.Object, Now);

        var generation = Assert.Single(plan.Generations);
        Assert.Equal(handOver, generation.ActivatesWhen);
        Assert.Equal(handOver + Lifetime, generation.RetiresWhen);
        Assert.Equal(handOver + Lifetime + RetirementGrace, generation.ExpiresWhen);
        Assert.Equal(TenantKeyChangeReasons.Successor, generation.Reason);
    }

    [Fact]
    public void Plan_WhenTheActiveKeyRetiresJustPastTheHorizon_GeneratesNoSuccessorYet()
    {
        SetupUsed("RS256");
        var active = CreateKey(
            "active",
            activatesWhen: Now.AddDays(-88),
            retiresWhen: Now + ActivationDelay + Interval + TimeSpan.FromSeconds(1)
        );
        SetupSatisfied(Rs256, active);

        var plan = Planner.Plan([Rs256], [active], MockSettings.Object, Now);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Plan_WhenTheSuccessorIsAlreadyPublished_GeneratesNoSecondSuccessor()
    {
        SetupUsed("RS256");
        var handOver = Now.AddHours(1);
        var active = CreateKey("active", activatesWhen: Now.AddDays(-90), retiresWhen: handOver);
        var successor = CreateKey(
            "successor",
            activatesWhen: handOver,
            retiresWhen: handOver + Lifetime,
            createdWhen: Now.AddDays(-2)
        );
        SetupSatisfied(Rs256, active, successor);

        var plan = Planner.Plan([Rs256], [active, successor], MockSettings.Object, Now);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Plan_WhenNoKeyIsActiveAndOneIsPublishedAhead_ActivatesThePublishedKeyAtOnce()
    {
        // The active key was deleted; the successor is published already, so activating it is the least disruptive
        // way to sign again, and no key is generated.
        SetupUsed("RS256");
        var later = CreateKey("later", activatesWhen: Now.AddDays(3), retiresWhen: Now.AddDays(93));
        var upcoming = CreateKey(
            "upcoming",
            activatesWhen: Now.AddDays(1),
            retiresWhen: Now.AddDays(91)
        );
        SetupSatisfied(Rs256, later, upcoming);

        var plan = Planner.Plan([Rs256], [later, upcoming], MockSettings.Object, Now);

        var activation = Assert.Single(plan.Activations);
        Assert.Same(upcoming, activation.Secret);
        Assert.Equal(Now, activation.ActivatesWhen);
        Assert.Empty(plan.Generations);
    }

    [Fact]
    public void Plan_WhenTheOnlyKeyIsRetired_GeneratesAKeyThatActivatesAtOnce()
    {
        // A retired key still verifies, so it is kept, and it signs nothing, so it does not serve the algorithm; the
        // policy is strict with no setup, because a key outside its active window is never judged by its material.
        SetupUsed("RS256");
        var retired = CreateKey(
            "retired",
            activatesWhen: Now.AddDays(-100),
            retiresWhen: Now.AddDays(-1)
        );

        var plan = Planner.Plan([Rs256], [retired], MockSettings.Object, Now);

        var generation = Assert.Single(plan.Generations);
        Assert.Equal(Now, generation.ActivatesWhen);
        Assert.Equal(TenantKeyChangeReasons.NoActiveKey, generation.Reason);
        Assert.Empty(plan.Removals);
    }

    [Fact]
    public void Plan_WhenAGeneratedKeyHasExpired_RemovesIt()
    {
        SetupUsed("RS256");
        var active = CreateKey(
            "active",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60)
        );
        var expired = CreateKey(
            "expired",
            activatesWhen: Now.AddDays(-200),
            retiresWhen: Now.AddDays(-110),
            expiresWhen: Now
        );
        SetupSatisfied(Rs256, active, expired);

        var plan = Planner.Plan([Rs256], [active, expired], MockSettings.Object, Now);

        var removal = Assert.Single(plan.Removals);
        Assert.Same(expired, removal);
        Assert.Empty(plan.Generations);
    }

    [Fact]
    public void Plan_WhenASuppliedKeyHasExpired_KeepsItForTheOperator()
    {
        SetupUsed("RS256");
        var active = CreateKey(
            "active",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60)
        );
        var expired = CreateKey(
            "expired",
            activatesWhen: Now.AddDays(-200),
            retiresWhen: Now.AddDays(-110),
            expiresWhen: Now,
            origin: SecretOrigins.Supplied
        );
        SetupSatisfied(Rs256, active, expired);

        var plan = Planner.Plan([Rs256], [active, expired], MockSettings.Object, Now);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Plan_WhenTheSettingsNoLongerUseAnAlgorithm_RetiresItsGeneratedKeyForTheGrace()
    {
        SetupUsed("ES256");
        var unused = CreateKey(
            "unused",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60)
        );
        var active = CreateKey(
            "active",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60),
            algorithm: "ES256"
        );
        SetupSatisfied(Es256, active);

        var plan = Planner.Plan([Rs256, Es256], [unused, active], MockSettings.Object, Now);

        var retirement = Assert.Single(plan.Retirements);
        Assert.Same(unused, retirement.Secret);
        Assert.Equal(Now, retirement.RetiresWhen);
        Assert.Equal(Now + RetirementGrace, retirement.ExpiresWhen);
        Assert.Empty(plan.Generations);
    }

    [Fact]
    public void Plan_WhenAnUnusedKeyExpiresBeforeTheGraceEnds_KeepsItsOwnExpiry()
    {
        SetupUsed("ES256");
        var unused = CreateKey(
            "unused",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(1),
            expiresWhen: Now.AddDays(2)
        );
        var active = CreateKey(
            "active",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60),
            algorithm: "ES256"
        );
        SetupSatisfied(Es256, active);

        var plan = Planner.Plan([Rs256, Es256], [unused, active], MockSettings.Object, Now);

        var retirement = Assert.Single(plan.Retirements);
        Assert.Equal(Now.AddDays(2), retirement.ExpiresWhen);
    }

    [Fact]
    public void Plan_WhenAnUnusedKeyIsAlreadyRetiredOrSupplied_LeavesIt()
    {
        SetupUsed("ES256");
        var retired = CreateKey(
            "retired",
            activatesWhen: Now.AddDays(-100),
            retiresWhen: Now.AddDays(-1)
        );
        var supplied = CreateKey(
            "supplied",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60),
            origin: SecretOrigins.Supplied
        );
        var unbound = CreateKey(
            "unbound",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60),
            algorithm: null
        );
        var active = CreateKey(
            "active",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60),
            algorithm: "ES256"
        );
        SetupSatisfied(Es256, active);

        var plan = Planner.Plan(
            [Rs256, Es256],
            [retired, supplied, unbound, active],
            MockSettings.Object,
            Now
        );

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Plan_WhenTheGraceIsShorterThanTheLongestTokenLifetime_UsesTheLifetimePlusTheClockSkew()
    {
        // No token may outlive the key that verifies it, whatever the configured grace says.
        SetupUsed("RS256");
        SetupLifetimes(
            retirementGrace: TimeSpan.FromHours(1),
            clockSkew: TimeSpan.FromMinutes(5),
            tokenLifetimes: [TimeSpan.FromDays(1), TimeSpan.FromDays(10)]
        );

        var plan = Planner.Plan([Rs256], [], MockSettings.Object, Now);

        var generation = Assert.Single(plan.Generations);
        Assert.Equal(
            Now + Lifetime + TimeSpan.FromDays(10) + TimeSpan.FromMinutes(5),
            generation.ExpiresWhen
        );
    }

    [Fact]
    public void Plan_WhenTheOnlyServingKeyIsNotYetActive_ActivatesItAndPlansNoSuccessorFromItsRetirement()
    {
        // The activated key covers the algorithm until it retires, far past the horizon.
        SetupUsed("RS256");
        var upcoming = CreateKey(
            "upcoming",
            activatesWhen: Now.AddDays(1),
            retiresWhen: Now.AddDays(91)
        );
        SetupSatisfied(Rs256, upcoming);

        var plan = Planner.Plan([Rs256], [upcoming], MockSettings.Object, Now);

        Assert.Single(plan.Activations);
        Assert.Empty(plan.Generations);
    }

    #endregion

    #region HasServingKey Tests

    [Fact]
    public void HasServingKey_WhenAHeldKeyIsActiveOrUpcoming_IsTrue()
    {
        var upcoming = CreateKey(
            "upcoming",
            activatesWhen: Now.AddDays(1),
            retiresWhen: Now.AddDays(91)
        );
        SetupSatisfied(Rs256, upcoming);

        Assert.True(Planner.HasServingKey(Rs256, [upcoming], Now));
    }

    [Fact]
    public void HasServingKey_WhenEveryHeldKeyIsRetiredOrExpired_IsFalse()
    {
        var retired = CreateKey("retired", activatesWhen: Now.AddDays(-100), retiresWhen: Now);
        var expired = CreateKey(
            "expired",
            activatesWhen: Now.AddDays(-200),
            retiresWhen: Now.AddDays(-110),
            expiresWhen: Now.AddDays(-1)
        );

        Assert.False(Planner.HasServingKey(Rs256, [retired, expired], Now));
    }

    [Fact]
    public void HasServingKey_WhenTheActiveKeyDoesNotSatisfyTheSpecification_IsFalse()
    {
        var active = CreateKey(
            "active",
            activatesWhen: Now.AddDays(-30),
            retiresWhen: Now.AddDays(60)
        );
        SetupSatisfied(Es256, active);

        Assert.False(Planner.HasServingKey(Rs256, [active], Now));
    }

    #endregion

    #region Helpers

    private static PersistedSecret CreateKey(
        string secretId,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        DateTimeOffset? expiresWhen = null,
        DateTimeOffset? createdWhen = null,
        string? algorithm = "RS256",
        string origin = SecretOrigins.Generated
    ) =>
        TenantKeyTestData.CreateSecret(
            secretId,
            createdWhen ?? activatesWhen,
            activatesWhen,
            retiresWhen,
            expiresWhen ?? retiresWhen + RetirementGrace,
            algorithm,
            origin
        );

    private void SetupUsed(params string[] algorithms)
    {
        object? value = algorithms.ToList();
        MockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingNames.IdTokenSigningAlgValuesSupported, out value)
            )
            .Returns(true);
    }

    private void SetupLifetimes(
        TimeSpan retirementGrace,
        TimeSpan? clockSkew = null,
        TimeSpan[]? tokenLifetimes = null
    )
    {
        MockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.TenantKeyLifetime)).Returns(Lifetime);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TenantKeyActivationDelay))
            .Returns(ActivationDelay);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TenantKeyRetirementGrace))
            .Returns(retirementGrace);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.ClockSkew))
            .Returns(clockSkew ?? TimeSpan.Zero);

        var names = OpenIdOptions.Tenant.KeyRollover.TokenLifetimeSettings;
        for (var i = 0; i < (tokenLifetimes?.Length ?? 0); i++)
        {
            object? lifetime = tokenLifetimes![i];
            MockSettings.Setup(x => x.TryGetValue(names[i], out lifetime)).Returns(true);
        }
    }

    // The policy judges a key by its material and algorithm, which these tests leave to it: a key satisfies the
    // specification its algorithm names and no other. One strict setup answers every question from that set.
    private void SetupSatisfied(TenantKeySpecification specification, params PersistedSecret[] keys)
    {
        foreach (var key in keys)
        {
            Satisfied.Add((specification, key));
        }

        if (IsPolicySetUp)
            return;

        IsPolicySetUp = true;
        MockPolicy
            .Setup(x =>
                x.IsSatisfiedBy(It.IsAny<TenantKeySpecification>(), It.IsAny<PersistedSecret>())
            )
            .Returns(
                (TenantKeySpecification candidate, PersistedSecret secret) =>
                    Satisfied.Contains((candidate, secret))
            )
            .Verifiable();
    }

    private HashSet<(TenantKeySpecification, PersistedSecret)> Satisfied { get; } = [];
    private bool IsPolicySetUp { get; set; }

    #endregion
}
