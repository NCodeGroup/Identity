// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Exceptions;

namespace NCode.Identity.Cose;

/// <summary>
/// The exception that is thrown when a CBOR Object Signing and Encryption (COSE) message, map or key cannot be read or
/// does not satisfy the rules of RFC 9052 and of this library. The <see cref="FailureCode"/> names the rule, so a
/// caller acts on the code and never on the message text. The message is fixed text and never carries message bytes or
/// content.
/// </summary>
[PublicAPI]
public sealed class CoseException : JoseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoseException"/> class.
    /// </summary>
    /// <param name="failureCode">The machine-readable code that names why the message is refused, typically one of the
    /// <see cref="CoseFailureCodes"/>.</param>
    /// <param name="message">The human-readable description of the refusal.</param>
    public CoseException(string failureCode, string message)
        : base(message)
    {
        FailureCode = failureCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CoseException"/> class with the exception that caused it.
    /// </summary>
    /// <param name="failureCode">The machine-readable code that names why the message is refused, typically one of the
    /// <see cref="CoseFailureCodes"/>.</param>
    /// <param name="message">The human-readable description of the refusal.</param>
    /// <param name="inner">The exception that caused the refusal.</param>
    public CoseException(string failureCode, string message, Exception inner)
        : base(message, inner)
    {
        FailureCode = failureCode;
    }

    /// <summary>
    /// Gets the machine-readable code that names why the message is refused.
    /// </summary>
    public string FailureCode { get; }
}
