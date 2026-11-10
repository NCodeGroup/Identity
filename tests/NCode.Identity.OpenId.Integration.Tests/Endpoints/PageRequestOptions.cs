// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Provides the optional headers of a page request that <see cref="InteractiveUiLocalesTests"/> sends.
/// </summary>
internal readonly record struct PageRequestOptions
{
    /// <summary>
    /// Gets the value of the <c>Accept-Language</c> header; <see langword="null"/> omits the header.
    /// </summary>
    public string? AcceptLanguage { get; init; }

    /// <summary>
    /// Gets the value of the session cookie; <see langword="null"/> sends no session cookie.
    /// </summary>
    public string? SessionCookie { get; init; }
}
