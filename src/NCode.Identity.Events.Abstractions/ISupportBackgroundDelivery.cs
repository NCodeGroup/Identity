// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events;

/// <summary>
/// Marks an <see cref="IEventHandler{TEvent}"/> that should be invoked on a background worker rather
/// than inline on the publishing path, so a slow or external sink never blocks the caller.
/// </summary>
/// <remarks>
/// Background delivery is only active when it has been enabled (see the background event delivery
/// registration); otherwise a handler marked for background delivery runs inline. Because background
/// delivery crosses the request boundary, a marked handler observes only what the event payload
/// carries, not ambient request state.
/// </remarks>
[PublicAPI]
public interface ISupportBackgroundDelivery;
