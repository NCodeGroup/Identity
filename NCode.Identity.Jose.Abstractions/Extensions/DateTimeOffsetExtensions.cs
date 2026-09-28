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
