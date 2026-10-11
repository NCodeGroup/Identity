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
using NCode.Registration;

namespace NCode.Identity.Events;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the event-publishing library. It is
/// minted by <c>AddEventServices</c>; the event opt-ins (<c>AddEventHandler</c>, <c>AddBackgroundEventDelivery</c>)
/// require its builder so they cannot be added without the event library.
/// </summary>
[PublicAPI]
public readonly struct EventsLibrary : IRegistrationMarker<EventsLibrary>;
