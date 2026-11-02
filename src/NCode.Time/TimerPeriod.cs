// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Time;

/// <summary>
/// Provides the range of periods that a timer accepts: a <see cref="PeriodicTimer"/> refuses a period shorter than one
/// millisecond, and <see cref="TimeProvider.CreateTimer"/>, <see cref="PeriodicTimer"/> and a
/// <see cref="CancellationTokenSource"/> delay refuse one longer than 4,294,967,294 milliseconds, about 49.7 days.
/// </summary>
[PublicAPI]
public static class TimerPeriod
{
    /// <summary>
    /// Gets the shortest period that a timer accepts, which is one millisecond.
    /// </summary>
    public static TimeSpan MinValue { get; } = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// Gets the longest period that a timer accepts, which is 4,294,967,294 milliseconds.
    /// </summary>
    public static TimeSpan MaxValue { get; } = TimeSpan.FromMilliseconds(uint.MaxValue - 1L);

    /// <summary>
    /// Determines whether a period is one that a timer accepts.
    /// </summary>
    /// <param name="period">The period to check.</param>
    /// <returns><see langword="true"/> if <paramref name="period"/> is between <see cref="MinValue"/> and <see cref="MaxValue"/>, inclusive; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(TimeSpan period) => period >= MinValue && period <= MaxValue;
}
