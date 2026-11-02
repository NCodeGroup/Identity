// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.RemoteApis;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiTests : BaseTests
{
    private static BffRemoteApi CreateApi(params string[] allowedMethods) =>
        new(
            "/api/orders",
            "https://api.example.com",
            new BffTokenRequest(BffTokenRequirementNames.User),
            allowedMethods,
            TimeSpan.FromSeconds(30),
            new HttpMessageInvoker(new HttpClientHandler())
        );

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsTheProperties()
    {
        var api = CreateApi("get");

        Assert.Equal("/api/orders", api.Path);
        Assert.Equal("https://api.example.com", api.Destination);
        Assert.Equal(BffTokenRequirementNames.User, api.TokenRequest.Requirement);
        Assert.Equal(TimeSpan.FromSeconds(30), api.RequestConfig.ActivityTimeout);
        Assert.NotNull(api.Invoker);
        Assert.Equal(["GET"], api.AllowedMethods);
    }

    #endregion

    #region AllowsMethod Tests

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public void AllowsMethod_NoAllowedMethods_AllowsEveryMethod(string method)
    {
        var api = CreateApi();

        Assert.True(api.AllowsMethod(method));
    }

    [Theory]
    [InlineData("GET", true)]
    [InlineData("get", true)]
    [InlineData("POST", true)]
    [InlineData("DELETE", false)]
    public void AllowsMethod_AllowedMethods_AllowsOnlyThem(string method, bool expected)
    {
        var api = CreateApi("get", "Post");

        Assert.Equal(expected, api.AllowsMethod(method));
    }

    #endregion

    #region AllowHeader Tests

    [Fact]
    public void AllowHeader_ListsTheMethodsInOrder()
    {
        var api = CreateApi("post", "GET", "delete");

        Assert.Equal("DELETE, GET, POST", api.AllowHeader);
    }

    #endregion
}
