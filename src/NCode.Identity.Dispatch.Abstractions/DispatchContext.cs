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
/// Carries the ambient state of a single execution attempt for a dispatched job, passed to the
/// <see cref="IDispatchJobHandler{TJob}"/> so a handler can make a delivery idempotent and reason about retries.
/// </summary>
[PublicAPI]
public readonly record struct DispatchContext
{
    /// <summary>
    /// Gets the stable identifier of the dispatched job that is constant across every retry attempt. A handler that
    /// performs an at-least-once side effect should key its de-duplication on this value, since a dispatcher adapter
    /// may redeliver the same job.
    /// </summary>
    public required Guid DispatchId { get; init; }

    /// <summary>
    /// Gets the one-based number of the current execution attempt.
    /// </summary>
    public required int Attempt { get; init; }

    /// <summary>
    /// Gets the maximum number of attempts the dispatcher will make before abandoning the job.
    /// </summary>
    public required int MaxAttempts { get; init; }
}
