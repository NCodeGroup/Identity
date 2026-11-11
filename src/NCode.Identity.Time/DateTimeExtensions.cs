// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Time;

/// <summary>
/// Provides extension methods for <see cref="DateTime"/> to truncate to the nearest second.
/// </summary>
[PublicAPI]
public static class DateTimeExtensions
{
    /// <param name="dateTime">The <see cref="DateTime"/> to truncate.</param>
    extension(DateTime dateTime)
    {
        /// <summary>
        /// Truncates the <see cref="DateTime"/> to the nearest second.
        /// </summary>
        /// <returns>A <see cref="DateTime"/> truncated to the nearest second.</returns>
        public DateTime WithPrecisionInSeconds() =>
            new(dateTime.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, dateTime.Kind);
    }
}
