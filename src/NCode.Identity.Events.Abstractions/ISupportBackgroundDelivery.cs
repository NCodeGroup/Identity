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
