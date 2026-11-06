// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Teams;
using Xunit;

namespace NCode.Identity.OpenId.Core.Teams;

public sealed class OpenIdTeamExtensionsTests : BaseTests
{
    private Mock<OpenIdTeam> CreateMockTeam(
        string teamId,
        string concurrencyToken,
        Mock<OpenIdTeam>? parent = null
    )
    {
        var mockTeam = CreateStrictMock<OpenIdTeam>();
        mockTeam.SetupGet(x => x.TeamId).Returns(teamId).Verifiable();
        mockTeam.SetupGet(x => x.ConcurrencyToken).Returns(concurrencyToken).Verifiable();
        mockTeam.SetupGet(x => x.Parent).Returns(parent?.Object).Verifiable();
        return mockTeam;
    }

    #region GetRevisionLineage Tests

    [Fact]
    public void GetRevisionLineage_WithATopLevelTeam_ReturnsTheTeamsRevision()
    {
        var mockTeam = CreateMockTeam("acme", "acme-ct");

        var lineage = mockTeam.Object.GetRevisionLineage();

        Assert.Equal(new TeamRevision("acme", "acme-ct"), Assert.Single(lineage));
    }

    [Fact]
    public void GetRevisionLineage_WithATeamTwoLevelsDeep_ReturnsTheTeamFirstThenEachAncestor()
    {
        var mockTop = CreateMockTeam("acme", "acme-ct");
        var mockMiddle = CreateMockTeam("acme-web", "acme-web-ct", mockTop);
        var mockTeam = CreateMockTeam("acme-web-eu", "acme-web-eu-ct", mockMiddle);

        var lineage = mockTeam.Object.GetRevisionLineage();

        Assert.Equal(
            [
                new TeamRevision("acme-web-eu", "acme-web-eu-ct"),
                new TeamRevision("acme-web", "acme-web-ct"),
                new TeamRevision("acme", "acme-ct"),
            ],
            lineage.ToArray()
        );
    }

    #endregion
}
