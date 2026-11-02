// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Events;

/// <summary>
/// A <see cref="RecordingEventHandler{TEvent}"/> that opts into background delivery.
/// </summary>
internal sealed class BackgroundRecordingEventHandler<TEvent>(
    string name,
    IList<string> invocationOrder
) : RecordingEventHandler<TEvent>(name, invocationOrder), ISupportBackgroundDelivery
    where TEvent : IEvent;
