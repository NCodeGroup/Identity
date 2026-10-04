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

namespace NCode.Identity.Events;

/// <summary>
/// Marker interface implemented by every event payload that can be published through
/// <see cref="IEventPublisher"/> and observed by an <see cref="IEventHandler{TEvent}"/>.
/// </summary>
/// <remarks>
/// An event is a pure notification: its handlers observe that something happened and must not
/// influence the outcome of the operation that raised it. Event payloads are immutable value-like
/// types (a <c>readonly record struct</c> or <c>record</c>).
/// </remarks>
[PublicAPI]
public interface IEvent;
