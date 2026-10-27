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
