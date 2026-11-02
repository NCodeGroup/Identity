// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Represents the failure to obtain an access token, with the HTTP status code and reason to answer with.
/// </summary>
[PublicAPI]
public sealed class BffTokenUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffTokenUnavailableException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to answer with.</param>
    /// <param name="reason">The reason that no token is available.</param>
    public BffTokenUnavailableException(int statusCode, string reason)
        : base(reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        StatusCode = statusCode;
        Reason = reason;
    }

    /// <summary>
    /// Gets the HTTP status code to answer with.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the reason that no token is available.
    /// </summary>
    public string Reason { get; }
}
