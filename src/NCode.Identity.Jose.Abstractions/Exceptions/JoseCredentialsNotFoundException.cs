// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Exceptions;

/// <summary>
/// Represents errors that occur when the <c>Jose</c> implementation is unable to locate a signing or encryption credential.
/// </summary>
[PublicAPI]
[Serializable]
public class JoseCredentialsNotFoundException : JoseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JoseCredentialsNotFoundException"/> class.
    /// </summary>
    public JoseCredentialsNotFoundException()
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseCredentialsNotFoundException"/> class with the specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public JoseCredentialsNotFoundException(string message)
        : base(message)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseCredentialsNotFoundException"/> class with the specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception,
    /// or a null reference if no inner exception is specified.</param>
    public JoseCredentialsNotFoundException(string message, Exception inner)
        : base(message, inner)
    {
        // nothing
    }
}
