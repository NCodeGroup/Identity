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

namespace NCode.Identity.Dispatch;

/// <summary>
/// Describes how a job should be scheduled and retried when it is handed to the <see cref="IJobDispatcher"/>. The
/// values are advisory hints the active dispatcher adapter honors as closely as its backend allows (the in-process
/// adapter applies them directly; a broker or scheduler adapter maps them onto its native retry and delay primitives).
/// </summary>
[PublicAPI]
public sealed record DispatchOptions
{
    /// <summary>
    /// Gets the shared default options: five attempts with exponential back-off starting at two seconds, and no
    /// initial delay.
    /// </summary>
    public static DispatchOptions Default { get; } = new();

    /// <summary>
    /// Gets the maximum number of execution attempts before the job is abandoned. Must be at least one. The default
    /// is <c>5</c>.
    /// </summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>
    /// Gets the back-off delay applied before the first retry. Subsequent retries multiply this by
    /// <see cref="BackoffFactor"/>, capped at <see cref="MaxBackoff"/>. The default is two seconds.
    /// </summary>
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets the multiplier applied to the back-off delay after each failed attempt. The default is <c>2.0</c>
    /// (exponential back-off).
    /// </summary>
    public double BackoffFactor { get; init; } = 2.0;

    /// <summary>
    /// Gets the ceiling for the computed back-off delay between retries. The default is five minutes.
    /// </summary>
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets an optional delay to wait before the first execution attempt, or <c>null</c> to run as soon as possible.
    /// </summary>
    public TimeSpan? Delay { get; init; }
}
