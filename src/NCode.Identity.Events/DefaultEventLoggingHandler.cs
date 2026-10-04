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

using Microsoft.Extensions.Logging;
using NCode.Identity.Events.Logging;

namespace NCode.Identity.Events;

/// <summary>
/// A universal subscriber that emits a low-level trace breadcrumb naming the type of every published
/// event. A generic handler only sees an opaque <see cref="IEvent"/>, so this records that an event
/// fired, not its contents; actionable, structured logging comes from handlers of a known contract
/// such as <see cref="DefaultAuditLoggingHandler"/>.
/// </summary>
internal class DefaultEventLoggingHandler(ILogger<DefaultEventLoggingHandler> logger)
    : IEventHandler<IEvent>,
        ISupportHandlerPriority
{
    private ILogger<DefaultEventLoggingHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public int HandlerPriority => DefaultHandlerPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(IEvent @event, CancellationToken cancellationToken)
    {
        Logger.EventPublished(@event.GetType().FullName ?? @event.GetType().Name);
        return ValueTask.CompletedTask;
    }
}
