// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// The exception that is thrown when a request for a key that a key management service holds is refused: the request
/// names a secret type a key management service cannot hold, a remote key provider that is not registered or no
/// algorithm row of the provider, the public half the service returns is not a key of the row's type, size and curve,
/// or the key does not prove that it holds the private half.
/// </summary>
/// <remarks>
/// <see cref="Exception.Message"/> is the reason. A failure of the key management service or its client is never this
/// exception.
/// </remarks>
// See ADR-1479.
[PublicAPI]
public sealed class RemoteKeyRefusedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteKeyRefusedException"/> class.
    /// </summary>
    public RemoteKeyRefusedException()
        : base("The remote key request was refused.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteKeyRefusedException"/> class with the specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RemoteKeyRefusedException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteKeyRefusedException"/> class with the specified
    /// error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public RemoteKeyRefusedException(string message, Exception innerException)
        : base(message, innerException) { }
}
