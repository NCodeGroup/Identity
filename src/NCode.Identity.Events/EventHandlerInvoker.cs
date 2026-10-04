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

namespace NCode.Identity.Events;

/// <summary>
/// Invokes a resolved <see cref="IEventHandler{TEvent}"/> for a published event whose concrete handler
/// type is known only at runtime, bridging the generic handler contract to a non-generic call.
/// </summary>
/// <param name="handler">The resolved handler instance.</param>
/// <param name="event">The event to hand to the handler.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
internal delegate ValueTask EventHandlerInvoker(
    object handler,
    IEvent @event,
    CancellationToken cancellationToken
);
