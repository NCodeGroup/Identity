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
