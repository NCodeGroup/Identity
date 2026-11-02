// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
