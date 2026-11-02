// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Caching;

/// <summary>
/// The cached state of one key. Its properties are written only while holding <see cref="Gate"/>.
/// </summary>
internal sealed class RefreshingCacheEntry<TValue>
    where TValue : class
{
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public TValue? Value { get; set; }

    public DateTimeOffset FetchedAt { get; set; }

    public DateTimeOffset LastAttemptAt { get; set; }
}
