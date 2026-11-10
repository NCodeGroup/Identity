// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Provides the optional claims of a request object that <see cref="AuthorizationRequestObjectTests"/> signs.
/// </summary>
internal readonly record struct RequestObjectOptions
{
    /// <summary>
    /// Gets how long the request object is valid; <see langword="null"/> is five minutes.
    /// </summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>
    /// Gets the <c>response_mode</c> claim; <see langword="null"/> omits it.
    /// </summary>
    public string? ResponseMode { get; init; }

    /// <summary>
    /// Gets the <c>redirect_uri</c> claim; <see langword="null"/> omits it.
    /// </summary>
    public string? RedirectUri { get; init; }
}
