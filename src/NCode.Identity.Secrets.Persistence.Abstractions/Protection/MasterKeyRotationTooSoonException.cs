// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// The exception that is thrown when the master key of an owner rotates again before every replica has loaded the
/// active key.
/// </summary>
/// <remarks>
/// A rotation within twice the refresh interval of the previous one protects nothing and only adds a retired key to
/// rewrap and check.
/// </remarks>
// See ADR-1407.
[PublicAPI]
public sealed class MasterKeyRotationTooSoonException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MasterKeyRotationTooSoonException"/> class.
    /// </summary>
    public MasterKeyRotationTooSoonException()
        : base("The master key rotated too recently to rotate again.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MasterKeyRotationTooSoonException"/> class with the specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public MasterKeyRotationTooSoonException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MasterKeyRotationTooSoonException"/> class with the specified
    /// error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public MasterKeyRotationTooSoonException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MasterKeyRotationTooSoonException"/> class.
    /// </summary>
    /// <param name="owner">The owner whose master key rotated too recently.</param>
    /// <param name="retryAfter">The time until the owner's master key may rotate again.</param>
    public MasterKeyRotationTooSoonException(SecretOwner owner, TimeSpan retryAfter)
        : base(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The master key of '{owner}' rotated too recently; it may rotate again in {retryAfter}."
            )
        )
    {
        Owner = owner;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets the owner whose master key rotated too recently.
    /// </summary>
    public SecretOwner Owner { get; }

    /// <summary>
    /// Gets the time until the owner's master key may rotate again.
    /// </summary>
    public TimeSpan RetryAfter { get; }
}
