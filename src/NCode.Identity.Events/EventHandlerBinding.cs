// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Events;

/// <summary>
/// Associates a closed <see cref="IEventHandler{TEvent}"/> service type with the delegate that invokes
/// it, forming one entry of a runtime event type's cached dispatch plan.
/// </summary>
/// <param name="HandlerServiceType">The closed <see cref="IEventHandler{TEvent}"/> service type to resolve.</param>
/// <param name="Invoker">The delegate that invokes a resolved handler of that service type.</param>
internal readonly record struct EventHandlerBinding(
    Type HandlerServiceType,
    EventHandlerInvoker Invoker
);
