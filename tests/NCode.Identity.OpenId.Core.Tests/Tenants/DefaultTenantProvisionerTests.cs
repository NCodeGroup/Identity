// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DefaultTenantProvisionerTests : IDisposable
{
    private const string RootTenantId = "root";
    private const string TeamId = "platform";
    private const string TeamDisplayName = "Platform Team";
    private const string RootDisplayName = "Control Plane";
    private const string WorkloadTenantId = "acme";
    private const string WorkloadIssuer = "https://op.example/acme";

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private Mock<IMediator> MockMediator { get; }
    private Mock<ITeamProvisioner> MockTeamProvisioner { get; }
    private DefaultTenantProvisioner Provisioner { get; }

    public DefaultTenantProvisionerTests()
    {
        MockStoreManagerFactory = Mocks.Create<IStoreManagerFactory>();
        MockStoreManager = Mocks.Create<IStoreManager>();
        MockTenantStore = Mocks.Create<ITenantStore>();
        MockAmbientTenantAccessor = Mocks.Create<IAmbientTenantAccessor>();
        MockTenantResolver = Mocks.Create<ITenantResolver>();
        MockMediator = Mocks.Create<IMediator>();
        MockTeamProvisioner = Mocks.Create<ITeamProvisioner>();

        Provisioner = new DefaultTenantProvisioner(
            MockStoreManagerFactory.Object,
            MockAmbientTenantAccessor.Object,
            MockTenantResolver.Object,
            Options.Create(
                new TenantResolutionOptions
                {
                    Root =
                    {
                        TeamId = TeamId,
                        TeamDisplayName = TeamDisplayName,
                        TenantId = RootTenantId,
                        DisplayName = RootDisplayName,
                    },
                }
            ),
            MockMediator.Object,
            MockTeamProvisioner.Object
        );
    }

    public void Dispose() => Mocks.Verify();

    private static PersistedTeam CreateTeam() =>
        new()
        {
            TeamId = TeamId,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = TeamDisplayName,
            Settings = null!,
        };

    private static PersistedTenant CreateTenant(string tenantId, string? issuer = null) =>
        new()
        {
            TenantId = tenantId,
            TeamId = "platform",
            Issuer = issuer,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = null!,
            Secrets = null!,
        };

    private void SetupTeam() =>
        MockTeamProvisioner
            .Setup(x =>
                x.EnsureCreatedAsync(TeamId, TeamDisplayName, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateTeam())
            .Verifiable();

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

    private void SetupGetTenant(string tenantId, params PersistedTenant?[] results)
    {
        var queue = new Queue<PersistedTenant?>(results);
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 1 ? queue.Dequeue() : queue.Peek())
            .Verifiable();
    }

    private Mock<IDisposable> SetupScope()
    {
        var scope = Mocks.Create<IDisposable>();
        scope.Setup(x => x.Dispose()).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(It.IsAny<string>()))
            .Returns(scope.Object)
            .Verifiable();
        return scope;
    }

    private void SetupSeed(List<SeedTenantCommand> seeded)
    {
        SetupScope();
        MockMediator
            .Setup(x => x.SendAsync(It.IsAny<SeedTenantCommand>(), It.IsAny<CancellationToken>()))
            .Callback((SeedTenantCommand command, CancellationToken _) => seeded.Add(command))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    // Each commit takes the next outcome: an exception fails it, null lets it succeed.
    private void SetupSeedCommits(List<SeedTenantCommand> seeded, params Exception?[] outcomes)
    {
        SetupScope();
        MockMediator
            .Setup(x => x.SendAsync(It.IsAny<SeedTenantCommand>(), It.IsAny<CancellationToken>()))
            .Callback((SeedTenantCommand command, CancellationToken _) => seeded.Add(command))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var commits = 0;
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
                outcomes[commits++] is { } failure
                    ? ValueTask.FromException(failure)
                    : ValueTask.CompletedTask
            )
            .Verifiable();
    }

    #region EnsureCreatedAsync Tests

    [Fact]
    public async Task EnsureCreatedAsync_WhenTenantExists_ReturnsItWithoutWriting()
    {
        SetupTeam();
        var existing = CreateTenant(WorkloadTenantId, WorkloadIssuer);
        SetupStoreManager();
        SetupGetTenant(WorkloadTenantId, existing);

        var result = await Provisioner.EnsureCreatedAsync(
            TeamId,
            WorkloadTenantId,
            "Acme",
            CancellationToken.None
        );

        Assert.Same(existing, result);
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenTenantIsNew_CreatesItAtItsDefaultAddress()
    {
        SetupTeam();
        const string workloadAlias = "https://acme.mtls.example";

        SetupStoreManager();
        SetupGetTenant(WorkloadTenantId, (PersistedTenant?)null);
        MockTenantResolver
            .Setup(x => x.GetDefaultIssuerAsync(WorkloadTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkloadIssuer)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.GetDefaultMtlsEndpointAliasAuthorityAsync(
                    WorkloadTenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(workloadAlias)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.ValidateTenantAsync(
                    WorkloadTenantId,
                    WorkloadIssuer,
                    workloadAlias,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();
        PersistedTenant? added = null;
        MockTenantStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTenant>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedTenant tenant, CancellationToken _) => added = tenant)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Provisioner.EnsureCreatedAsync(
            TeamId,
            WorkloadTenantId,
            "Acme",
            CancellationToken.None
        );

        Assert.NotNull(added);
        Assert.Same(added, result);
        Assert.Equal(WorkloadTenantId, result.TenantId);
        Assert.Equal(TeamId, result.TeamId);
        Assert.Equal(WorkloadIssuer, result.Issuer);
        Assert.Equal(workloadAlias, result.MtlsEndpointAliasAuthority);
        Assert.Equal("Acme", result.DisplayName);
        Assert.False(result.IsDisabled);
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenDefaultAddressCannotBeServed_ThrowsWithoutWriting()
    {
        SetupTeam();
        SetupStoreManager();
        SetupGetTenant(WorkloadTenantId, (PersistedTenant?)null);
        MockTenantResolver
            .Setup(x => x.GetDefaultIssuerAsync(WorkloadTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.GetDefaultMtlsEndpointAliasAuthorityAsync(
                    WorkloadTenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((string?)null)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.ValidateTenantAsync(
                    WorkloadTenantId,
                    null,
                    null,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new TenantValidationFailure { Detail = "The tenant requires an issuer." },
            ])
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Provisioner.EnsureCreatedAsync(
                TeamId,
                WorkloadTenantId,
                "Acme",
                CancellationToken.None
            )
        );

        Assert.Contains(
            "The tenant requires an issuer.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenConcurrentCreatorWins_ReturnsTheirTenant()
    {
        SetupTeam();
        var concurrent = CreateTenant(WorkloadTenantId, WorkloadIssuer);
        SetupStoreManager();
        SetupGetTenant(WorkloadTenantId, null, concurrent);
        MockTenantResolver
            .Setup(x => x.GetDefaultIssuerAsync(WorkloadTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkloadIssuer)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.GetDefaultMtlsEndpointAliasAuthorityAsync(
                    WorkloadTenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((string?)null)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.ValidateTenantAsync(
                    WorkloadTenantId,
                    WorkloadIssuer,
                    null,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();
        MockTenantStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTenant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("duplicate key"))
            .Verifiable();

        var result = await Provisioner.EnsureCreatedAsync(
            TeamId,
            WorkloadTenantId,
            "Acme",
            CancellationToken.None
        );

        Assert.Same(concurrent, result);
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenInsertFailsAndTenantIsStillMissing_Rethrows()
    {
        SetupTeam();
        SetupStoreManager();
        SetupGetTenant(WorkloadTenantId, (PersistedTenant?)null);
        MockTenantResolver
            .Setup(x => x.GetDefaultIssuerAsync(WorkloadTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkloadIssuer)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.GetDefaultMtlsEndpointAliasAuthorityAsync(
                    WorkloadTenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((string?)null)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.ValidateTenantAsync(
                    WorkloadTenantId,
                    WorkloadIssuer,
                    null,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();
        MockTenantStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTenant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var failure = new InvalidOperationException("database unavailable");
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Provisioner.EnsureCreatedAsync(
                TeamId,
                WorkloadTenantId,
                "Acme",
                CancellationToken.None
            )
        );

        Assert.Same(failure, exception);
    }

    #endregion

    #region SeedAsync Tests

    [Fact]
    public async Task SeedAsync_WhenRootTenant_SeedsItInTheRootPlaneOnly()
    {
        var root = CreateTenant(RootTenantId);
        var seeded = new List<SeedTenantCommand>();
        SetupStoreManager();
        SetupGetTenant(RootTenantId, root);
        SetupSeed(seeded);

        var result = await Provisioner.SeedAsync(RootTenantId, CancellationToken.None);

        Assert.Same(root, result);
        var command = Assert.Single(seeded);
        Assert.Same(root, command.Context.Tenant);
        Assert.Equal(TenantPlane.Root, command.Context.Plane);
        Assert.Same(MockStoreManager.Object, command.Context.StoreManager);
        MockAmbientTenantAccessor.Verify(x => x.BeginScope(RootTenantId), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WhenWorkloadTenant_EnsuresAndSeedsRootFirst()
    {
        SetupTeam();
        var root = CreateTenant(RootTenantId);
        var workload = CreateTenant(WorkloadTenantId, WorkloadIssuer);
        var seeded = new List<SeedTenantCommand>();
        SetupStoreManager();
        SetupGetTenant(RootTenantId, root);
        SetupGetTenant(WorkloadTenantId, workload);
        SetupSeed(seeded);

        var result = await Provisioner.SeedAsync(WorkloadTenantId, CancellationToken.None);

        Assert.Same(workload, result);
        Assert.Collection(
            seeded,
            command =>
            {
                Assert.Same(root, command.Context.Tenant);
                Assert.Equal(TenantPlane.Root, command.Context.Plane);
            },
            command =>
            {
                Assert.Same(workload, command.Context.Tenant);
                Assert.Equal(TenantPlane.Workload, command.Context.Plane);
            }
        );
    }

    [Fact]
    public async Task SeedAsync_WhenWorkloadTenantAndRootIsMissing_CreatesRootWithItsConfiguredName()
    {
        SetupTeam();
        const string rootIssuer = "https://op.example/root";
        var workload = CreateTenant(WorkloadTenantId, WorkloadIssuer);
        var seeded = new List<SeedTenantCommand>();
        SetupStoreManager();
        SetupGetTenant(WorkloadTenantId, workload);
        MockTenantResolver
            .Setup(x => x.GetDefaultIssuerAsync(RootTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rootIssuer)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.GetDefaultMtlsEndpointAliasAuthorityAsync(
                    RootTenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((string?)null)
            .Verifiable();
        MockTenantResolver
            .Setup(x =>
                x.ValidateTenantAsync(
                    RootTenantId,
                    rootIssuer,
                    null,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();
        PersistedTenant? added = null;
        MockTenantStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTenant>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedTenant tenant, CancellationToken _) => added = tenant)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(RootTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => added)
            .Verifiable();
        SetupSeed(seeded);

        await Provisioner.SeedAsync(WorkloadTenantId, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(RootTenantId, added.TenantId);
        Assert.Equal(RootDisplayName, added.DisplayName);
        Assert.Equal(rootIssuer, added.Issuer);
        Assert.Equal(2, seeded.Count);
    }

    [Fact]
    public async Task SeedAsync_WhenTenantIsMissing_ThrowsNotFound()
    {
        SetupStoreManager();
        SetupGetTenant(RootTenantId, (PersistedTenant?)null);
        SetupScope();

        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await Provisioner.SeedAsync(RootTenantId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task SeedAsync_WhenCommitLosesToAConcurrentSeed_SeedsAgainInAFreshUnitOfWork()
    {
        var first = CreateTenant(RootTenantId);
        var second = CreateTenant(RootTenantId);
        var seeded = new List<SeedTenantCommand>();
        SetupStoreManager();
        SetupGetTenant(RootTenantId, first, second);
        SetupSeedCommits(seeded, new InvalidOperationException("concurrency conflict"), null);

        var result = await Provisioner.SeedAsync(RootTenantId, CancellationToken.None);

        Assert.Same(second, result);
        Assert.Collection(
            seeded,
            command => Assert.Same(first, command.Context.Tenant),
            command => Assert.Same(second, command.Context.Tenant)
        );
    }

    [Fact]
    public async Task SeedAsync_WhenCommitKeepsFailing_SurfacesTheLastFailure()
    {
        var root = CreateTenant(RootTenantId);
        var seeded = new List<SeedTenantCommand>();
        var firstFailure = new InvalidOperationException("concurrency conflict");
        var lastFailure = new InvalidOperationException("database unavailable");
        SetupStoreManager();
        SetupGetTenant(RootTenantId, root);
        SetupSeedCommits(seeded, firstFailure, lastFailure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Provisioner.SeedAsync(RootTenantId, CancellationToken.None)
        );

        Assert.Same(lastFailure, exception);
        Assert.Equal(2, seeded.Count);
    }

    [Fact]
    public async Task SeedAsync_WhenCommitIsCanceled_DoesNotSeedAgain()
    {
        var root = CreateTenant(RootTenantId);
        var seeded = new List<SeedTenantCommand>();
        SetupStoreManager();
        SetupGetTenant(RootTenantId, root);
        SetupSeedCommits(seeded, new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Provisioner.SeedAsync(RootTenantId, CancellationToken.None)
        );

        Assert.Single(seeded);
    }

    #endregion
}
