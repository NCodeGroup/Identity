// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// The exception that is thrown when an owner that cannot bring a root key tries to set one.
/// </summary>
/// <remarks>
/// A registered <see cref="IOwnerRootKeyPolicy"/> refused the owner. <see cref="Exception.Message"/> is the reason that
/// the policy gave.
/// </remarks>
// See ADR-1414.
[PublicAPI]
public sealed class OwnerRootKeyNotAllowedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerRootKeyNotAllowedException"/> class.
    /// </summary>
    public OwnerRootKeyNotAllowedException()
        : base("The owner cannot bring a root key.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerRootKeyNotAllowedException"/> class with the specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public OwnerRootKeyNotAllowedException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerRootKeyNotAllowedException"/> class with the specified
    /// error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public OwnerRootKeyNotAllowedException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerRootKeyNotAllowedException"/> class.
    /// </summary>
    /// <param name="owner">The owner that cannot bring a root key.</param>
    /// <param name="reason">The reason that the policy gave.</param>
    public OwnerRootKeyNotAllowedException(SecretOwner owner, string reason)
        : base(reason)
    {
        Owner = owner;
        Reason = reason;
    }

    /// <summary>
    /// Gets the owner that cannot bring a root key.
    /// </summary>
    public SecretOwner Owner { get; }

    /// <summary>
    /// Gets the reason that the policy gave.
    /// </summary>
    public string Reason { get; } = string.Empty;
}
