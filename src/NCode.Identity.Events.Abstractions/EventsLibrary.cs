// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Registration;

namespace NCode.Identity.Events;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the event-publishing library. It is
/// minted by <c>AddEventServices</c>; the event opt-ins (<c>AddEventHandler</c>, <c>AddBackgroundEventDelivery</c>)
/// require its builder so they cannot be added without the event library.
/// </summary>
[PublicAPI]
public readonly struct EventsLibrary : IRegistrationMarker<EventsLibrary>;
