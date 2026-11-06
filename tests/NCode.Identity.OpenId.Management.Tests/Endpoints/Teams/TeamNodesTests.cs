// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Authorization;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

public sealed class TeamNodesTests
{
    private const string ControlPlaneTenantId = "root";

    #region GetNodeAbove Tests

    [Fact]
    public void GetNodeAbove_WithAParent_IsTheParentTeamNode()
    {
        var node = TeamNodes.GetNodeAbove(ControlPlaneTenantId, "parent-team");

        Assert.Equal(ControlPlaneTenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Team, node.ResourceType);
        Assert.Equal("parent-team", node.ResourceId);
    }

    [Fact]
    public void GetNodeAbove_WithoutAParent_IsThePlatformNode()
    {
        var node = TeamNodes.GetNodeAbove(ControlPlaneTenantId, null);

        Assert.Equal(ControlPlaneTenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Platform, node.ResourceType);
        Assert.Equal(OpenIdConstants.PlatformResourceId, node.ResourceId);
    }

    #endregion
}
