// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents an error response returned by an OpenID provider.
/// </summary>
/// <param name="error">The error code.</param>
/// <param name="statusCode">The HTTP status code of the response.</param>
[PublicAPI]
public sealed class ProtocolError(string error, int statusCode)
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string Error { get; } = error;

    /// <summary>
    /// Gets the HTTP status code of the response.
    /// </summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>
    /// Gets the human-readable error description, if any.
    /// </summary>
    public string? ErrorDescription { get; init; }

    /// <summary>
    /// Gets the URI of a page with more information about the error, if any.
    /// </summary>
    public string? ErrorUri { get; init; }
}
