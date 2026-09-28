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
/// Provides extension methods for <see cref="TimeProvider"/> to truncate to the nearest second.
/// </summary>
[PublicAPI]
public static class TimeProviderExtensions
{
    /// <param name="timeProvider">The <see cref="TimeProvider"/> instance.</param>
    extension(TimeProvider timeProvider)
    {
        /// <summary>
        /// Gets the current system timestamp truncated to the nearest second.
        /// </summary>
        /// <returns>A <see cref="long"/> representing the current system timestamp truncated to the nearest second.</returns>
        public long GetTimestampWithPrecisionInSeconds()
        {
            var timestamp = timeProvider.GetTimestamp();
            var frequency = timeProvider.TimestampFrequency;
            return timestamp / frequency * frequency;
        }

        /// <summary>
        /// Gets the current system time in UTC truncated to the nearest second.
        /// </summary>
        /// <returns>A <see cref="DateTimeOffset"/> representing the current system time in UTC truncated to the nearest second.</returns>
        public DateTimeOffset GetUtcNowWithPrecisionInSeconds() =>
            new DateTime(timeProvider.GetTimestampWithPrecisionInSeconds(), DateTimeKind.Utc);
    }
}
