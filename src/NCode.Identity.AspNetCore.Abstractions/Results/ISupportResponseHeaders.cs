// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.Results;

/// <summary>
/// Provides the ability to associate additional HTTP response headers with the current instance.
/// Used to emit protocol headers such as <c>WWW-Authenticate</c> challenges (RFC 7235) alongside a rendered response.
/// </summary>
[PublicAPI]
public interface ISupportResponseHeaders
{
    /// <summary>
    /// Gets the collection of HTTP response headers to write when rendering the response, keyed by header name,
    /// or <see langword="null"/> when no additional headers have been added.
    /// </summary>
    IReadOnlyDictionary<string, StringValues>? ResponseHeaders { get; }

    /// <summary>
    /// Adds or replaces an HTTP response header to be written when rendering the response.
    /// </summary>
    /// <param name="name">The name of the HTTP response header.</param>
    /// <param name="value">The value of the HTTP response header.</param>
    void SetResponseHeader(string name, StringValues value);
}
