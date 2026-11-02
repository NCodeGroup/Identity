// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Dispatch.InProcess;

/// <summary>
/// Bridges the generic <see cref="IDispatchJobHandler{TJob}"/> contract to a non-generic call so the worker can invoke
/// a handler whose concrete job type is known only at runtime.
/// </summary>
/// <param name="handler">The resolved handler instance.</param>
/// <param name="job">The job to execute.</param>
/// <param name="context">The <see cref="DispatchContext"/> describing the current attempt.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
internal delegate ValueTask JobHandlerInvoker(
    object handler,
    IDispatchJob job,
    DispatchContext context,
    CancellationToken cancellationToken
);
