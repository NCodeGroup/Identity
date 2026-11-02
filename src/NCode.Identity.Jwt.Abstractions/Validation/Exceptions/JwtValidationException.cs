// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Exceptions;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Represents errors that occur during <c>JSON Web Token (JWT)</c> validation.
/// </summary>
[PublicAPI]
[Serializable]
public class JwtValidationException : JoseException
{
    private const string DefaultMessage = "Token validation failed.";

    /// <summary>
    /// Gets the machine-readable code of the reason the token failed validation. The default is
    /// <see cref="JwtValidationFailureCodes.InvalidToken"/>.
    /// </summary>
    public string FailureCode { get; init; } = JwtValidationFailureCodes.InvalidToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtValidationException"/> class with a default error message.
    /// </summary>
    public JwtValidationException()
        : base(DefaultMessage)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtValidationException"/> class with the specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public JwtValidationException(string message)
        : base(message)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtValidationException"/> class with the specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception,
    /// or a null reference if no inner exception is specified.</param>
    public JwtValidationException(string message, Exception inner)
        : base(message, inner)
    {
        // nothing
    }
}
