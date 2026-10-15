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

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.Clients;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public sealed class DefaultClientAssertionReplayCacheTests
{
    private IDistributedCache DistributedCache { get; } =
        new ServiceCollection()
            .AddDistributedMemoryCache()
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();

    #region TryRecordAsync Tests

    [Fact]
    public async Task TryRecordAsync_WhenFirstSeen_RecordsThenRejectsReplay()
    {
        var cache = new DefaultClientAssertionReplayCache(DistributedCache);
        var expiresWhen = DateTimeOffset.UtcNow.AddMinutes(1);

        var first = await cache.TryRecordAsync(
            "client-1",
            "jti-1",
            expiresWhen,
            CancellationToken.None
        );
        var second = await cache.TryRecordAsync(
            "client-1",
            "jti-1",
            expiresWhen,
            CancellationToken.None
        );

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task TryRecordAsync_WhenDifferentJti_BothRecorded()
    {
        var cache = new DefaultClientAssertionReplayCache(DistributedCache);
        var expiresWhen = DateTimeOffset.UtcNow.AddMinutes(1);

        var first = await cache.TryRecordAsync(
            "client-1",
            "jti-1",
            expiresWhen,
            CancellationToken.None
        );
        var second = await cache.TryRecordAsync(
            "client-1",
            "jti-2",
            expiresWhen,
            CancellationToken.None
        );

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public async Task TryRecordAsync_WhenSameJtiDifferentClient_BothRecorded()
    {
        var cache = new DefaultClientAssertionReplayCache(DistributedCache);
        var expiresWhen = DateTimeOffset.UtcNow.AddMinutes(1);

        var first = await cache.TryRecordAsync(
            "client-1",
            "jti-1",
            expiresWhen,
            CancellationToken.None
        );
        var second = await cache.TryRecordAsync(
            "client-2",
            "jti-1",
            expiresWhen,
            CancellationToken.None
        );

        Assert.True(first);
        Assert.True(second);
    }

    #endregion
}
