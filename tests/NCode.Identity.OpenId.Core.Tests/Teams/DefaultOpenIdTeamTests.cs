// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using Moq.Protected;
using NCode.Disposables;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Settings;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Teams;

public sealed class DefaultOpenIdTeamTests : BaseTests
{
    private static async ValueTask<(DefaultOpenIdTeam Team, Func<int> Releases)> CreateTeamAsync()
    {
        var releases = 0;
        var provider = Mock.Of<IReadOnlySettingCollectionProvider>();

        // Only the team holds the settings provider once the creator releases its own reference.
        await using var owner = AsyncSharedReference.Create(
            provider,
            _ =>
            {
                Interlocked.Increment(ref releases);
                return ValueTask.CompletedTask;
            }
        );

        var team = new DefaultOpenIdTeam(
            "acme",
            "team-ct",
            "Acme",
            isDisabled: false,
            "/acme/",
            owner,
            default,
            PropertyBagFactory.Create()
        );

        return (team, () => Volatile.Read(ref releases));
    }

    private Mock<OpenIdTeam> CreateMockParentTeam() => CreateStrictMock<OpenIdTeam>();

    #region Parent Tests

    [Fact]
    public async Task Parent_WithAParentReference_IsTheParentTeam()
    {
        var parent = CreateMockParentTeam();
        parent
            .Protected()
            .Setup<ValueTask>("DisposeAsyncCore")
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        await using var parentOwner = AsyncSharedReference.Create(parent.Object);
        var provider = Mock.Of<IReadOnlySettingCollectionProvider>();
        await using var settingsOwner = AsyncSharedReference.Create(provider);

        await using var team = new DefaultOpenIdTeam(
            "acme-web",
            "team-ct",
            "Acme Web",
            isDisabled: false,
            "/acme/acme-web/",
            settingsOwner,
            parentOwner.AddReference(),
            PropertyBagFactory.Create()
        );

        Assert.Same(parent.Object, team.Parent);
        Assert.Equal("/acme/acme-web/", team.Path);
    }

    [Fact]
    public async Task Parent_WithoutAParentReference_IsNull()
    {
        var (team, _) = await CreateTeamAsync();

        Assert.Null(team.Parent);
        Assert.Equal("/acme/", team.Path);
        await team.DisposeAsync();
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_ReleasesTheSettingsLeaseOnce()
    {
        var (team, releases) = await CreateTeamAsync();
        Assert.Equal(0, releases());

        await team.DisposeAsync();

        Assert.Equal(1, releases());
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_ReleasesTheSettingsLeaseOnce()
    {
        var (team, releases) = await CreateTeamAsync();

        await team.DisposeAsync();
        await team.DisposeAsync();

        Assert.Equal(1, releases());
    }

    [Fact]
    public async Task DisposeAsync_WithAParent_ReleasesTheParentReference()
    {
        var parentReleases = 0;
        var parent = CreateMockParentTeam();
        await using var parentOwner = AsyncSharedReference.Create(
            parent.Object,
            _ =>
            {
                Interlocked.Increment(ref parentReleases);
                return ValueTask.CompletedTask;
            }
        );
        var provider = Mock.Of<IReadOnlySettingCollectionProvider>();
        await using var settingsOwner = AsyncSharedReference.Create(provider);

        var team = new DefaultOpenIdTeam(
            "acme-web",
            "team-ct",
            "Acme Web",
            isDisabled: false,
            "/acme/acme-web/",
            settingsOwner,
            parentOwner.AddReference(),
            PropertyBagFactory.Create()
        );

        await parentOwner.DisposeAsync();
        Assert.Equal(0, Volatile.Read(ref parentReleases));

        await team.DisposeAsync();

        Assert.Equal(1, Volatile.Read(ref parentReleases));
    }

    #endregion
}
