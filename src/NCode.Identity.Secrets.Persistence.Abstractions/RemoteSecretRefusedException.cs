// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// The exception that is thrown when the secret generator refuses a request to create or bind a key that a key
/// management service holds: the request names a secret type a key management service cannot hold, a remote key
/// provider that is not registered or no signing algorithm row of the provider, or the public half the service
/// returns is not a key of the row's type, size and curve.
/// </summary>
/// <remarks>
/// <see cref="Exception.Message"/> is the reason. A failure of the key management service or its client is never this
/// exception.
/// </remarks>
// See ADR-1476.
[PublicAPI]
public sealed class RemoteSecretRefusedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteSecretRefusedException"/> class.
    /// </summary>
    public RemoteSecretRefusedException()
        : base("The secret generator refused the remote key request.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteSecretRefusedException"/> class with the specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RemoteSecretRefusedException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteSecretRefusedException"/> class with the specified
    /// error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public RemoteSecretRefusedException(string message, Exception innerException)
        : base(message, innerException) { }
}
