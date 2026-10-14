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
/// The adapter port for scheduling background jobs for out-of-band, at-least-once execution. It decouples the code
/// that produces work from the strategy that runs it: the default adapter is an in-process, self-scheduling worker,
/// and a deployment can replace it wholesale with a durable or distributed backend (a transactional outbox, a message
/// broker such as RabbitMQ, or a scheduling cluster such as Quartz) by registering its own implementation, without any
/// change to the producers or the <see cref="IDispatchJobHandler{TJob}"/> handlers.
/// </summary>
[PublicAPI]
public interface IJobDispatcher
{
    /// <summary>
    /// Schedules a job for background execution by its registered <see cref="IDispatchJobHandler{TJob}"/>. The call
    /// returns once the job has been accepted by the dispatcher; the job then runs out of band, honoring the supplied
    /// scheduling and retry <paramref name="options"/>.
    /// </summary>
    /// <param name="job">The job to schedule.</param>
    /// <param name="options">
    /// The <see cref="DispatchOptions"/> describing how to schedule and retry the job, or <c>null</c> to use
    /// <see cref="DispatchOptions.Default"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the enqueue operation (not the eventual
    /// background execution).
    /// </param>
    /// <typeparam name="TJob">The type of job being scheduled.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous enqueue operation.</returns>
    ValueTask DispatchAsync<TJob>(
        TJob job,
        DispatchOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where TJob : class, IDispatchJob;
}
