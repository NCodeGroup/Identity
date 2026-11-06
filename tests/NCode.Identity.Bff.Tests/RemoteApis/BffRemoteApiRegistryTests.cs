// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.RemoteApis;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiRegistryTests : BaseTests
{
    #region TryRegister Tests

    [Fact]
    public void TryRegister_FirstPath_Succeeds()
    {
        var registry = new BffRemoteApiRegistry();

        var result = registry.TryRegister("/api/orders", out var overlapping);

        Assert.True(result);
        Assert.Null(overlapping);
    }

    [Theory]
    [InlineData("/api/orders")]
    [InlineData("/API/Orders")]
    [InlineData("/api/orders/items")]
    [InlineData("/api")]
    public void TryRegister_OverlappingPath_ReturnsTheOtherPath(string path)
    {
        var registry = new BffRemoteApiRegistry();
        registry.TryRegister("/api/orders", out _);

        var result = registry.TryRegister(path, out var overlapping);

        Assert.False(result);
        Assert.Equal("/api/orders", overlapping);
    }

    [Theory]
    [InlineData("/api/order")]
    [InlineData("/api/orders2")]
    [InlineData("/api/customers")]
    public void TryRegister_SiblingPath_Succeeds(string path)
    {
        var registry = new BffRemoteApiRegistry();
        registry.TryRegister("/api/orders", out _);

        var result = registry.TryRegister(path, out var overlapping);

        Assert.True(result);
        Assert.Null(overlapping);
    }

    [Fact]
    public void TryRegister_PathOverlapsAShorterRecordedPath_ReturnsTheOtherPath()
    {
        var registry = new BffRemoteApiRegistry();
        registry.TryRegister("/api", out _);

        var result = registry.TryRegister("/api/orders", out var overlapping);

        Assert.False(result);
        Assert.Equal("/api", overlapping);
    }

    #endregion
}
