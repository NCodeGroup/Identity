// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Xunit;

namespace NCode.AspNetCore.Tests;

public class NoStoreResponseExtensionsTests : BaseTests
{
    #region SetNoStore Tests

    [Fact]
    public void SetNoStore_Always_WritesNoStoreCacheControlAndNoCachePragma()
    {
        var response = new DefaultHttpContext().Response;

        response.SetNoStore();

        Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", response.Headers.Pragma.ToString());
    }

    [Fact]
    public void SetNoStore_WhenTheResponseIsCacheable_ReplacesTheCacheControl()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers.CacheControl = "public, max-age=3600";

        response.SetNoStore();

        Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl.ToString());
    }

    #endregion
}
