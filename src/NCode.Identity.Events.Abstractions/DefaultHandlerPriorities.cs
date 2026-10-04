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
/// Provides the standard priority bands used by handlers that implement
/// <see cref="ISupportHandlerPriority"/> to control their invocation order.
/// </summary>
/// <remarks>
/// Handlers are sorted in descending order, so <see cref="High"/> runs before the implicit default
/// priority of <c>0</c>, which runs before <see cref="Low"/>. The values are mutable to let an
/// application adjust the boundaries, though this is rarely needed.
/// </remarks>
[PublicAPI]
public static class DefaultHandlerPriorities
{
    /// <summary>
    /// Gets or sets the low priority band, for handlers that should run late. The default is <c>-100</c>.
    /// </summary>
    public static int Low { get; set; } = -100;

    /// <summary>
    /// Gets or sets the high priority band, for handlers that should run early. The default is <c>100</c>.
    /// </summary>
    public static int High { get; set; } = 100;
}
