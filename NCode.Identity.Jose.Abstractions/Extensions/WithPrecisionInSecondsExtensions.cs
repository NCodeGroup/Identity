#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.Jose.Extensions;

/// <summary>
/// Provides extension methods for various date time primitives to truncate to the nearest second.
/// </summary>
[PublicAPI]
public static class WithPrecisionInSecondsExtensions
{
    /// <summary>
    /// Truncates the <see cref="DateTime"/> to the nearest second.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime"/> to truncate.</param>
    /// <returns>A <see cref="DateTime"/> truncated to the nearest second.</returns>
    public static DateTime WithPrecisionInSeconds(this DateTime dateTime) =>
        new(dateTime.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, dateTime.Kind);

    /// <summary>
    /// Truncates the <see cref="DateTimeOffset"/> to the nearest second.
    /// </summary>
    /// <param name="dateTimeOffset">The <see cref="DateTimeOffset"/> to truncate.</param>
    /// <returns>A <see cref="DateTimeOffset"/> truncated to the nearest second.</returns>
    public static DateTimeOffset WithPrecisionInSeconds(this DateTimeOffset dateTimeOffset) => new(
        dateTimeOffset.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond,
        dateTimeOffset.Offset);

    /// <summary>
    /// Gets the current system timestamp truncated to the nearest second.
    /// </summary>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> instance.</param>
    /// <returns>A <see cref="long"/> representing the current system timestamp truncated to the nearest second.</returns>
    public static long GetTimestampWithPrecisionInSeconds(this TimeProvider timeProvider)
    {
        var timestamp = timeProvider.GetTimestamp();
        var frequency = timeProvider.TimestampFrequency;
        return timestamp / frequency * frequency;
    }

    /// <summary>
    /// Gets the current system time in UTC truncated to the nearest second.
    /// </summary>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> instance.</param>
    /// <returns>A <see cref="DateTimeOffset"/> representing the current system time in UTC truncated to the nearest second.</returns>
    public static DateTimeOffset GetUtcNowWithPrecisionInSeconds(this TimeProvider timeProvider) =>
        new DateTime(GetTimestampWithPrecisionInSeconds(timeProvider), DateTimeKind.Utc);
}
