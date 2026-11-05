// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides one rule that decides whether a request belongs to a frontend. Rules form an open strategy collection that
/// is selected by <see cref="Name"/>.
/// </summary>
[PublicAPI]
public interface IBffFrontendSelector
{
    /// <summary>
    /// Gets the name that selects this rule. Names are compared ordinally.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Validates what a frontend configures for this rule.
    /// </summary>
    /// <param name="selection">The selection of the frontend.</param>
    /// <returns>The failure messages, without a configuration key, or an empty list when the selection is valid.</returns>
    IReadOnlyList<string> Validate(BffFrontendSelection selection);

    /// <summary>
    /// Determines whether two frontends that use this rule could select the same request.
    /// </summary>
    /// <param name="first">The selection of the first frontend.</param>
    /// <param name="second">The selection of the second frontend.</param>
    /// <returns><see langword="true"/> when one request could belong to both frontends; otherwise, <see langword="false"/>.</returns>
    bool Overlaps(BffFrontendSelection first, BffFrontendSelection second);

    /// <summary>
    /// Determines whether a request belongs to a frontend.
    /// </summary>
    /// <param name="request">The current HTTP request.</param>
    /// <param name="selection">The selection of the frontend.</param>
    /// <returns><see langword="true"/> when the request belongs to the frontend; otherwise, <see langword="false"/>.</returns>
    bool IsMatch(HttpRequest request, BffFrontendSelection selection);
}
