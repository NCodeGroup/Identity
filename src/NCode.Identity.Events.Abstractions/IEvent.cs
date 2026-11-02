// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
