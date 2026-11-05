// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Teams;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Teams;

public sealed class DefaultTeamProvisionerTests : IDisposable
{
    private const string TeamId = "platform";

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private DefaultTeamProvisioner Provisioner { get; }

    public DefaultTeamProvisionerTests()
    {
        MockStoreManagerFactory = Mocks.Create<IStoreManagerFactory>();
        MockStoreManager = Mocks.Create<IStoreManager>();
        MockTeamStore = Mocks.Create<ITeamStore>();

        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        Provisioner = new DefaultTeamProvisioner(MockStoreManagerFactory.Object);
    }

    public void Dispose() => Mocks.Verify();

    private static PersistedTeam CreateTeam(string displayName) =>
        new()
        {
            TeamId = TeamId,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = displayName,
            Settings = null!,
        };

    private void SetupGetTeam(params PersistedTeam?[] results)
    {
        var queue = new Queue<PersistedTeam?>(results);
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 1 ? queue.Dequeue() : queue.Peek())
            .Verifiable();
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenTeamExists_ReturnsItWithoutWriting()
    {
        var existing = CreateTeam("Existing");
        SetupGetTeam(existing);

        var result = await Provisioner.EnsureCreatedAsync(
            TeamId,
            "Platform",
            CancellationToken.None
        );

        Assert.Same(existing, result);
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenTeamIsNew_CreatesIt()
    {
        SetupGetTeam((PersistedTeam?)null);
        PersistedTeam? added = null;
        MockTeamStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTeam>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedTeam team, CancellationToken _) => added = team)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Provisioner.EnsureCreatedAsync(
            TeamId,
            "Platform",
            CancellationToken.None
        );

        Assert.NotNull(added);
        Assert.Same(added, result);
        Assert.Equal(TeamId, result.TeamId);
        Assert.Equal("Platform", result.DisplayName);
        Assert.False(result.IsDisabled);
        Assert.Equal(TeamId, result.Settings.TeamId);
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenConcurrentCreatorWins_ReturnsTheirTeam()
    {
        var concurrent = CreateTeam("Theirs");
        SetupGetTeam(null, concurrent);
        MockTeamStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTeam>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("duplicate key"))
            .Verifiable();

        var result = await Provisioner.EnsureCreatedAsync(
            TeamId,
            "Platform",
            CancellationToken.None
        );

        Assert.Same(concurrent, result);
    }
}
