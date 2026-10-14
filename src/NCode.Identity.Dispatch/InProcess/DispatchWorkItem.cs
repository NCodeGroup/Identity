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

namespace NCode.Identity.Dispatch.InProcess;

/// <summary>
/// Represents a single job together with the scheduling state the in-process dispatcher tracks across retries.
/// </summary>
internal sealed record DispatchWorkItem
{
    /// <summary>
    /// Gets the stable identifier assigned when the job was first dispatched; constant across retries.
    /// </summary>
    public required Guid DispatchId { get; init; }

    /// <summary>
    /// Gets the job to execute.
    /// </summary>
    public required IDispatchJob Job { get; init; }

    /// <summary>
    /// Gets the scheduling and retry options for the job.
    /// </summary>
    public required DispatchOptions Options { get; init; }

    /// <summary>
    /// Gets the one-based number of the current execution attempt.
    /// </summary>
    public required int Attempt { get; init; }
}
