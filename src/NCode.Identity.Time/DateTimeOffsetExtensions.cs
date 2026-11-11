// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Time;

/// <summary>
/// Provides extension methods for <see cref="DateTimeOffset"/> to truncate to the nearest second.
/// </summary>
[PublicAPI]
public static class DateTimeOffsetExtensions
{
    /// <param name="dateTimeOffset">The <see cref="DateTimeOffset"/> to truncate.</param>
    extension(DateTimeOffset dateTimeOffset)
    {
        /// <summary>
        /// Truncates the <see cref="DateTimeOffset"/> to the nearest second.
        /// </summary>
        /// <returns>A <see cref="DateTimeOffset"/> truncated to the nearest second.</returns>
        public DateTimeOffset WithPrecisionInSeconds() =>
            new(
                dateTimeOffset.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond,
                dateTimeOffset.Offset
            );
    }
}
