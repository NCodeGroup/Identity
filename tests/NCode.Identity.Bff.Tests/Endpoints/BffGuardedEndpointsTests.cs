// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Endpoints;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class BffGuardedEndpointsTests : BaseTests
{
    private static Endpoint CreateEndpoint(params object[] metadata) =>
        new(null, new EndpointMetadataCollection(metadata), "test");

    #region IsHub Tests

    [Fact]
    public void IsHub_HubAttribute_ReturnsTrue()
    {
        var endpoint = CreateEndpoint(new BffHubAttribute());

        Assert.True(BffGuardedEndpoints.IsHub(endpoint));
    }

    [Fact]
    public void IsHub_ApiAndHubAttributes_ReturnsFalse()
    {
        var endpoint = CreateEndpoint(new BffApiAttribute(), new BffHubAttribute());

        Assert.False(BffGuardedEndpoints.IsHub(endpoint));
    }

    #endregion

    #region IsPage Tests

    [Fact]
    public void IsPage_HubAndPageAttributes_ReturnsFalse()
    {
        var endpoint = CreateEndpoint(new BffHubAttribute(), new BffPageAttribute());

        Assert.False(BffGuardedEndpoints.IsPage(endpoint));
    }

    #endregion

    #region IsGuarded Tests

    [Fact]
    public void IsGuarded_HubAttribute_ReturnsTrue()
    {
        var endpoint = CreateEndpoint(new BffHubAttribute());

        Assert.True(BffGuardedEndpoints.IsGuarded(endpoint));
    }

    #endregion
}
