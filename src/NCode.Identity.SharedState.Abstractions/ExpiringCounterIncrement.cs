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

namespace NCode.Identity.SharedState;

/// <summary>
/// Represents one counter of an ordered batch that <see cref="IExpiringCounterSet"/> increments in one call. The counter
/// named <see cref="Key"/> is incremented as <see cref="IExpiringCounterSet.IncrementAsync"/> does, so an expiry that is
/// not in the future counts nothing and reports a count of zero. The counter named <see cref="ReferenceKey"/> is only
/// read, and only when <see cref="ReferenceWeight"/> is positive. The batch stops after this counter when its count plus
/// <see cref="ReferenceWeight"/> times the reference counter's count exceeds <see cref="Limit"/>.
/// </summary>
// See ADR-0246.
[PublicAPI]
public readonly record struct ExpiringCounterIncrement
{
    /// <summary>
    /// Gets the name of the counter to increment. Keys are compared ordinally.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the instant after which a counter this increment starts is no longer present. An expiry that is not in the
    /// future counts nothing.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets the largest weighted count that lets the batch continue; a greater count stops the batch after this counter.
    /// </summary>
    public required long Limit { get; init; }

    /// <summary>
    /// Gets the name of the counter that is read, and not incremented, to weigh into the count, or
    /// <see langword="null"/> when no counter is read.
    /// </summary>
    public string? ReferenceKey { get; init; }

    /// <summary>
    /// Gets the factor that scales the reference counter's count before it is added to the count. The reference counter
    /// is read only when the weight is positive.
    /// </summary>
    public double ReferenceWeight { get; init; }
}
