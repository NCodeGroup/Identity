// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Disposables;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Settings;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Teams;

public sealed class DefaultOpenIdTeamTests
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
            owner,
            PropertyBagFactory.Create()
        );

        return (team, () => Volatile.Read(ref releases));
    }

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

    #endregion
}
