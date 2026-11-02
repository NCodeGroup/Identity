// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents errors that occur while communicating with an OpenID provider.
/// </summary>
[PublicAPI]
public sealed class OpenIdProviderException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdProviderException"/> class with the specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public OpenIdProviderException(string message)
        : base(message)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdProviderException"/> class with the specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public OpenIdProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
        // nothing
    }
}
