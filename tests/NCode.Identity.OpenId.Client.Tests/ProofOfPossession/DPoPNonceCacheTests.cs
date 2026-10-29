#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using NCode.Identity.OpenId.Client.ProofOfPossession;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.ProofOfPossession;

public class DPoPNonceCacheTests
{
    #region Get Tests

    [Fact]
    public void Get_WhenNotSet_ReturnsNull()
    {
        var cache = new DPoPNonceCache();

        var result = cache.Get(new Uri("https://op.example.test/token"));

        Assert.Null(result);
    }

    [Fact]
    public void Get_ForOtherOrigin_ReturnsNull()
    {
        var cache = new DPoPNonceCache();
        cache.Set(new Uri("https://op.example.test/token"), "nonce-1");

        var result = cache.Get(new Uri("https://api.example.test/x"));

        Assert.Null(result);
    }

    #endregion

    #region Set Tests

    [Fact]
    public void Set_ThenGetForSameOrigin_ReturnsNonce()
    {
        var cache = new DPoPNonceCache();
        cache.Set(new Uri("https://op.example.test/token"), "nonce-1");

        var result = cache.Get(new Uri("https://op.example.test/par"));

        Assert.Equal("nonce-1", result);
    }

    [Fact]
    public void Set_WhenFull_ClearsBeforeAddingNewOrigin()
    {
        var cache = new DPoPNonceCache();
        for (var i = 0; i < DPoPNonceCache.MaxOrigins; i++)
            cache.Set(new Uri($"https://host{i}.example.test/x"), "old");

        cache.Set(new Uri("https://new.example.test/x"), "new");

        Assert.Null(cache.Get(new Uri("https://host0.example.test/x")));
        Assert.Equal("new", cache.Get(new Uri("https://new.example.test/x")));
    }

    #endregion
}
