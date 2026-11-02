// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Time;

/// <summary>
/// Provides extension methods for <see cref="TimeProvider"/> to truncate to the nearest second.
/// </summary>
[PublicAPI]
public static class TimeProviderExtensions
{
    /// <param name="timeProvider">The <see cref="TimeProvider"/> instance.</param>
    extension(TimeProvider timeProvider)
    {
        /// <summary>
        /// Gets the current wall-clock UTC time (in <see cref="DateTime.Ticks"/>) truncated to the nearest second.
        /// </summary>
        /// <returns>A <see cref="long"/> representing the current UTC time, in ticks, truncated to the nearest second.</returns>
        public long GetTimestampWithPrecisionInSeconds() =>
            timeProvider.GetUtcNowWithPrecisionInSeconds().UtcTicks;

        /// <summary>
        /// Gets the current system time in UTC truncated to the nearest second.
        /// </summary>
        /// <returns>A <see cref="DateTimeOffset"/> representing the current system time in UTC truncated to the nearest second.</returns>
        public DateTimeOffset GetUtcNowWithPrecisionInSeconds() =>
            timeProvider.GetUtcNow().WithPrecisionInSeconds();
    }
}
