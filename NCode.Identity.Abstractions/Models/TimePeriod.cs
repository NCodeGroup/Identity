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

namespace NCode.Identity.Models;

/// <summary>
/// Represents a time period with a start time and an optional end time.
/// </summary>
[PublicAPI]
public readonly record struct TimePeriod
{
    // DateTimeOffset StartTime, DateTimeOffset? EndTime

    /// <summary>
    /// Gets or sets the start time of the period.
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Gets or sets the optional end time of the period.
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// Gets duration (i.e. end time minus start time) of the time period.
    /// </summary>
    public TimeSpan? Duration => EndTime - StartTime;
}
