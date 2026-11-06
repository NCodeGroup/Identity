// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// Holds the frontend that the request was selected for, so the selection rules run once per request.
/// </summary>
/// <param name="frontend">The frontend of the request, or <see langword="null"/> when no frontend serves it.</param>
internal sealed class BffFrontendFeature(BffFrontend? frontend)
{
    /// <summary>
    /// Gets the frontend of the request, or <see langword="null"/> when no frontend serves it.
    /// </summary>
    public BffFrontend? Frontend { get; } = frontend;
}
