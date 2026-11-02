// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events;

/// <summary>
/// Allows an <see cref="IEventHandler{TEvent}"/> to specify the order in which it is invoked relative
/// to other handlers for the same published event.
/// </summary>
/// <remarks>
/// Handlers are sorted in descending priority order (higher values run first). A handler that does not
/// implement this interface is treated as priority <c>0</c>. See <see cref="DefaultHandlerPriorities"/>
/// for the standard priority bands.
/// </remarks>
[PublicAPI]
public interface ISupportHandlerPriority
{
    /// <summary>
    /// Gets the priority that determines this handler's invocation order; higher values run first.
    /// </summary>
    int HandlerPriority { get; }
}
