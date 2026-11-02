// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Represents errors that occur when a <see cref="SecretKey"/> is not found during <c>JSON Web Token (JWT)</c> validation.
/// The failure code defaults to <see cref="JwtValidationFailureCodes.KeyNotFound"/>.
/// </summary>
[PublicAPI]
[Serializable]
public class JwtSecretKeyNotFoundException : JwtValidationException
{
    private const string DefaultMessage =
        "Token validation failed. No keys were provided to validate the token.";

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtSecretKeyNotFoundException"/> class with a default error message.
    /// </summary>
    public JwtSecretKeyNotFoundException()
        : base(DefaultMessage)
    {
        FailureCode = JwtValidationFailureCodes.KeyNotFound;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtSecretKeyNotFoundException"/> class with the specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public JwtSecretKeyNotFoundException(string message)
        : base(message)
    {
        FailureCode = JwtValidationFailureCodes.KeyNotFound;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtSecretKeyNotFoundException"/> class with the specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception,
    /// or a null reference if no inner exception is specified.</param>
    public JwtSecretKeyNotFoundException(string message, Exception inner)
        : base(message, inner)
    {
        FailureCode = JwtValidationFailureCodes.KeyNotFound;
    }
}
