// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Dispatch;

/// <summary>
/// Executes a dispatched job of type <typeparamref name="TJob"/> when it is drained by the active
/// <see cref="IJobDispatcher"/> adapter. A handler should be idempotent — keyed on
/// <see cref="DispatchContext.DispatchId"/> — because dispatch is at-least-once and a job may be redelivered; throwing
/// from the handler signals failure and schedules a retry (up to the configured attempt limit).
/// </summary>
/// <typeparam name="TJob">The type of job this handler executes.</typeparam>
[PublicAPI]
public interface IDispatchJobHandler<in TJob>
    where TJob : class, IDispatchJob
{
    /// <summary>
    /// Executes the supplied job. Returning normally marks the attempt successful; throwing marks it failed and, while
    /// attempts remain, schedules a retry.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    /// <param name="context">The <see cref="DispatchContext"/> describing the current attempt.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask HandleAsync(TJob job, DispatchContext context, CancellationToken cancellationToken);
}
