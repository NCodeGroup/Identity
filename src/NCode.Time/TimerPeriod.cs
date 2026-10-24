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
