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
