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
/// Associates a closed <see cref="IEventHandler{TEvent}"/> service type with the delegate that invokes
/// it, forming one entry of a runtime event type's cached dispatch plan.
/// </summary>
/// <param name="HandlerServiceType">The closed <see cref="IEventHandler{TEvent}"/> service type to resolve.</param>
/// <param name="Invoker">The delegate that invokes a resolved handler of that service type.</param>
internal readonly record struct EventHandlerBinding(
    Type HandlerServiceType,
    EventHandlerInvoker Invoker
);
