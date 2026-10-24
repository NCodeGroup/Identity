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

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Describes a rate limit or a quota: the stage that judges it, the window it counts in, the settings that carry its
/// limit and overage, the partition a request counts in, and the usage meters an admitted request records. A package
/// contributes a descriptor through <c>ICollectionDataSource&lt;RateLimitDescriptor&gt;</c>, and the stage it names
/// judges the limit with the descriptors of the same stage in one counter-store call. Every partition lies within the
/// request's tenant, so a descriptor cannot count requests across tenants.
/// </summary>
// See ADR-0246.
[PublicAPI]
public sealed class RateLimitDescriptor
{
    /// <summary>
    /// Gets the name of the limit, which is the partition of its counters, the <c>partition</c> attribute of the
    /// rate-limiting metric, and unique among every registered descriptor.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the name of the stage that judges the limit, such as one of the <see cref="RateLimitStages"/>. The
    /// descriptors of a stage are judged in <see cref="Order"/>, then <see cref="Name"/>, and the first one that
    /// refuses the request ends the stage.
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// Gets the position of the limit among the descriptors of its stage; the lowest is judged first. A request that
    /// an earlier limit refuses is not counted against a later one.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the length of the limit's window. Windows start at multiples of the length counted from the start of
    /// <see cref="DateTimeOffset"/> time, so a window of one day starts at midnight UTC.
    /// </summary>
    public required TimeSpan Window { get; init; }

    /// <summary>
    /// Gets a value indicating whether the window slides. A sliding window estimates the count of the last
    /// <see cref="Window"/> from the current and the previous fixed window. A fixed window counts exactly, so it is
    /// the only kind that may carry an <see cref="OverageKey"/>.
    /// </summary>
    public bool IsSliding { get; init; }

    /// <summary>
    /// Gets the key of the setting that holds the most requests the limit admits in one window. A tenant or client
    /// that does not set it is not limited.
    /// </summary>
    public required SettingKey<int> LimitKey { get; init; }

    /// <summary>
    /// Gets the key of the setting that holds the most requests admitted past <see cref="LimitKey"/> in one window as
    /// billable overage, or <see langword="null"/> when the limit refuses at its limit. The window must be fixed.
    /// </summary>
    public SettingKey<int>? OverageKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether the limit applies only to a request that is metered, so that a request for
    /// public metadata does not count against it.
    /// </summary>
    public bool IsMeteredOnly { get; init; }

    /// <summary>
    /// Gets the usage meter recorded for every request that the stage and every later stage admit, or
    /// <see langword="null"/> when the limit records none.
    /// </summary>
    public string? UsageMeter { get; init; }

    /// <summary>
    /// Gets the usage meter recorded in addition to <see cref="UsageMeter"/> for a request admitted past the limit as
    /// overage, or <see langword="null"/> when the limit records none. It requires an <see cref="OverageKey"/>.
    /// </summary>
    public string? OverageUsageMeter { get; init; }

    /// <summary>
    /// Gets the function that selects the partition of the limit a request counts in, such as its remote address or its
    /// client. It returns <see langword="null"/> when the limit does not apply to the request, and an empty string
    /// when the limit counts every request in one partition.
    /// </summary>
    public required Func<RateLimitContext, string?> PartitionValueSelector { get; init; }
}
