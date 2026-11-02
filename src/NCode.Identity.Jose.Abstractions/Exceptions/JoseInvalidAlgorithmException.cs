// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Exceptions;

/// <summary>
/// Represents errors that occur when the <c>Jose</c> implementation is unable to determine what algorithm to use.
/// </summary>
[PublicAPI]
[Serializable]
public class JoseInvalidAlgorithmException : JoseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JoseInvalidAlgorithmException"/> class.
    /// </summary>
    public JoseInvalidAlgorithmException()
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseInvalidAlgorithmException"/> class with the specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public JoseInvalidAlgorithmException(string message)
        : base(message)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseInvalidAlgorithmException"/> class with the specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception,
    /// or a null reference if no inner exception is specified.</param>
    public JoseInvalidAlgorithmException(string message, Exception inner)
        : base(message, inner)
    {
        // nothing
    }
}
