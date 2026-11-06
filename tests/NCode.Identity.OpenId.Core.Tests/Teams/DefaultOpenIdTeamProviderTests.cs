// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Moq;
using Moq.Protected;
using NCode.Disposables;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Teams;
using NCode.Persistence.Stores;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Teams;

public sealed class DefaultOpenIdTeamProviderTests
{
    private static Mock<OpenIdTeam> CreateMockTeam()
    {
        var team = new Mock<OpenIdTeam>(MockBehavior.Strict);
        team.Protected().Setup<ValueTask>("DisposeAsyncCore").Returns(ValueTask.CompletedTask);
        return team;
    }

    private static PersistedTeam CreatePersistedTeam(string teamId = "platform") =>
        new()
        {
            TeamId = teamId,
            IsDisabled = false,
            DisplayName = "Platform",
            Settings = new PersistedTeamSettings
            {
                TeamId = teamId,
                ConcurrencyToken = "token",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
        };

    private static IStoreManagerFactory CreateStoreManagerFactory(
        MockRepository mocks,
        PersistedTeam? persistedTeam
    )
    {
        var teamStore = mocks.Create<ITeamStore>();
        teamStore
            .Setup(x => x.GetOrDefaultAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(persistedTeam)
            .Verifiable();
        var storeManager = mocks.Create<IStoreManager>();
        storeManager.Setup(x => x.GetStore<ITeamStore>()).Returns(teamStore.Object).Verifiable();
        storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        var factory = mocks.Create<IStoreManagerFactory>();
        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storeManager.Object)
            .Verifiable();
        return factory.Object;
    }

    private static IStoreManagerFactory CreateStoreManagerFactoryForTeams(
        MockRepository mocks,
        params PersistedTeam[] persistedTeams
    )
    {
        var teamStore = mocks.Create<ITeamStore>();
        foreach (var persistedTeam in persistedTeams)
        {
            teamStore
                .Setup(x =>
                    x.GetOrDefaultAsync(persistedTeam.TeamId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(persistedTeam)
                .Verifiable();
        }
        var storeManager = mocks.Create<IStoreManager>();
        storeManager.Setup(x => x.GetStore<ITeamStore>()).Returns(teamStore.Object).Verifiable();
        storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        var factory = mocks.Create<IStoreManagerFactory>();
        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storeManager.Object)
            .Verifiable();
        return factory.Object;
    }

    #region GetTeamAsync Tests

    [Fact]
    public async Task GetTeamAsync_WhenCached_ReturnsTheCachedTeam()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var team = CreateMockTeam();
        await using var owner = AsyncSharedReference.Create(team.Object);
        var cache = mocks.Create<IOpenIdTeamCache>();
        cache
            .Setup(x =>
                x.TryGetAsync("platform", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(owner.AddReference())
            .Verifiable();
        var provider = new DefaultOpenIdTeamProvider(
            mocks.Create<IStoreManagerFactory>().Object,
            cache.Object,
            mocks.Create<IOpenIdTeamFactory>().Object
        );

        await using var lease = await provider.GetTeamAsync(
            Mock.Of<OpenIdEnvironment>(),
            Mock.Of<OpenIdPlatform>(),
            "platform",
            Mock.Of<IPropertyBag>(),
            CancellationToken.None
        );

        Assert.Same(team.Object, lease.Value);
        mocks.Verify();
    }

    [Fact]
    public async Task GetTeamAsync_WhenCacheMisses_MaterializesAndCachesTheTeam()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var persistedTeam = CreatePersistedTeam();
        var team = CreateMockTeam();
        await using var created = AsyncSharedReference.Create(team.Object);
        var cache = mocks.Create<IOpenIdTeamCache>();
        AsyncSharedReferenceLease<OpenIdTeam> empty = default;
        cache
            .Setup(x =>
                x.TryGetAsync("platform", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(empty)
            .Verifiable();
        cache
            .Setup(x =>
                x.SetAsync(
                    "platform",
                    It.IsAny<AsyncSharedReferenceLease<OpenIdTeam>>(),
                    It.IsAny<IPropertyBag>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var factory = mocks.Create<IOpenIdTeamFactory>();
        factory
            .Setup(x =>
                x.CreateTeamAsync(
                    It.IsAny<OpenIdEnvironment>(),
                    It.IsAny<OpenIdPlatform>(),
                    It.IsAny<AsyncSharedReferenceLease<OpenIdTeam>>(),
                    persistedTeam,
                    It.IsAny<IPropertyBag>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(created.AddReference())
            .Verifiable();
        var provider = new DefaultOpenIdTeamProvider(
            CreateStoreManagerFactory(mocks, persistedTeam),
            cache.Object,
            factory.Object
        );

        await using var lease = await provider.GetTeamAsync(
            Mock.Of<OpenIdEnvironment>(),
            Mock.Of<OpenIdPlatform>(),
            "platform",
            Mock.Of<IPropertyBag>(),
            CancellationToken.None
        );

        Assert.Same(team.Object, lease.Value);
        mocks.Verify();
    }

    [Fact]
    public async Task GetTeamAsync_WhenTheTeamIsMissing_ThrowsNotFound()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var cache = mocks.Create<IOpenIdTeamCache>();
        AsyncSharedReferenceLease<OpenIdTeam> empty = default;
        cache
            .Setup(x =>
                x.TryGetAsync("platform", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(empty)
            .Verifiable();
        var provider = new DefaultOpenIdTeamProvider(
            CreateStoreManagerFactory(mocks, null),
            cache.Object,
            mocks.Create<IOpenIdTeamFactory>().Object
        );

        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await provider.GetTeamAsync(
                Mock.Of<OpenIdEnvironment>(),
                Mock.Of<OpenIdPlatform>(),
                "platform",
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            )
        );

        mocks.Verify();
    }

    [Fact]
    public async Task GetTeamAsync_WithAParentTeam_MaterializesTheParentFirst()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var parentPersisted = CreatePersistedTeam("acme");
        var childPersisted = CreatePersistedTeam("acme-web");
        childPersisted.ParentTeamId = "acme";
        childPersisted.AncestorTeamIds = ["acme"];

        var parentTeam = mocks.Create<OpenIdTeam>();
        parentTeam
            .Protected()
            .Setup<ValueTask>("DisposeAsyncCore")
            .Returns(ValueTask.CompletedTask);
        parentTeam.Setup(x => x.TeamId).Returns("acme").Verifiable();
        var childTeam = CreateMockTeam();
        await using var parentOwner = AsyncSharedReference.Create(parentTeam.Object);
        await using var childOwner = AsyncSharedReference.Create(childTeam.Object);

        var cache = mocks.Create<IOpenIdTeamCache>();
        AsyncSharedReferenceLease<OpenIdTeam> empty = default;
        cache
            .Setup(x =>
                x.TryGetAsync(
                    It.IsAny<string>(),
                    It.IsAny<IPropertyBag>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(empty)
            .Verifiable();
        cache
            .Setup(x =>
                x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<AsyncSharedReferenceLease<OpenIdTeam>>(),
                    It.IsAny<IPropertyBag>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        string? parentTeamIdSeenByTheFactory = null;
        var factory = mocks.Create<IOpenIdTeamFactory>();
        factory
            .Setup(x =>
                x.CreateTeamAsync(
                    It.IsAny<OpenIdEnvironment>(),
                    It.IsAny<OpenIdPlatform>(),
                    It.IsAny<AsyncSharedReferenceLease<OpenIdTeam>>(),
                    parentPersisted,
                    It.IsAny<IPropertyBag>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(parentOwner.AddReference())
            .Verifiable();
        factory
            .Setup(x =>
                x.CreateTeamAsync(
                    It.IsAny<OpenIdEnvironment>(),
                    It.IsAny<OpenIdPlatform>(),
                    It.IsAny<AsyncSharedReferenceLease<OpenIdTeam>>(),
                    childPersisted,
                    It.IsAny<IPropertyBag>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdEnvironment _,
                    OpenIdPlatform _,
                    AsyncSharedReferenceLease<OpenIdTeam> parent,
                    PersistedTeam _,
                    IPropertyBag _,
                    CancellationToken _
                ) => parentTeamIdSeenByTheFactory = parent.IsActive ? parent.Value.TeamId : null
            )
            .ReturnsAsync(childOwner.AddReference())
            .Verifiable();
        var provider = new DefaultOpenIdTeamProvider(
            CreateStoreManagerFactoryForTeams(mocks, parentPersisted, childPersisted),
            cache.Object,
            factory.Object
        );

        await using var lease = await provider.GetTeamAsync(
            Mock.Of<OpenIdEnvironment>(),
            Mock.Of<OpenIdPlatform>(),
            "acme-web",
            Mock.Of<IPropertyBag>(),
            CancellationToken.None
        );

        Assert.Same(childTeam.Object, lease.Value);
        Assert.Equal("acme", parentTeamIdSeenByTheFactory);
        mocks.Verify();
    }

    #endregion
}
