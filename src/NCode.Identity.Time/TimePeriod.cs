// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Time;

/// <summary>
/// Represents a time period with a start time and an optional end time.
/// </summary>
[PublicAPI]
public readonly record struct TimePeriod
{
    /// <summary>
    /// Gets the start time of the period.
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Gets the optional end time of the period.
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// Gets the duration (i.e. end time minus start time) of the time period.
    /// </summary>
    public TimeSpan? Duration => EndTime - StartTime;
}
