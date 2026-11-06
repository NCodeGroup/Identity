// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Persistence;

public class TeamPathsTests
{
    #region Format Tests

    [Fact]
    public void Format_WithTwoIds_ReturnsTheSlashDelimitedPath()
    {
        var path = TeamPaths.Format(["acme", "acme-web"]);

        Assert.Equal("/acme/acme-web/", path);
    }

    [Fact]
    public void Format_WithNoIds_Throws()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.Format([]));
    }

    [Fact]
    public void Format_WithAnIdHoldingASlash_Throws()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.Format(["acme", "acme/web"]));
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void Parse_WithAPath_ReturnsTheIdsTopLevelFirst()
    {
        var ids = TeamPaths.Parse("/acme/acme-web/");

        Assert.Equal(["acme", "acme-web"], ids);
    }

    [Fact]
    public void Parse_WithoutTheTrailingSlash_Throws()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.Parse("/acme/acme-web"));
    }

    [Fact]
    public void Parse_WithAnEmptySegment_Throws()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.Parse("/acme//acme-web/"));
    }

    #endregion
}
