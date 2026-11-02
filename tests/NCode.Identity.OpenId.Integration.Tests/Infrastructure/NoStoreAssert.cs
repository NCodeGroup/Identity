// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

public static class NoStoreAssert
{
    public static void IsNoStore(HttpResponseMessage response)
    {
        var cacheControl = response.Headers.CacheControl;
        Assert.True(
            cacheControl?.NoStore == true,
            $"Cache-Control lacks no-store: '{cacheControl}'."
        );

        var hasNoCachePragma = response.Headers.Pragma.Any(pragma => pragma.Name == "no-cache");
        Assert.True(hasNoCachePragma, "Pragma lacks no-cache.");
    }
}
