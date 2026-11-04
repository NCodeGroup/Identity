// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Exceptions;

namespace NCode.Identity.Jose.SdJwt;

/// <summary>
/// The exception that is thrown when a Selective Disclosure JWT (SD-JWT), one of its disclosures or its Key Binding JWT
/// cannot be read or does not satisfy the rules of RFC 9901. The <see cref="FailureCode"/> names the rule, so a caller
/// acts on the code and never on the message text.
/// </summary>
[PublicAPI]
public sealed class SdJwtException : JoseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SdJwtException"/> class.
    /// </summary>
    /// <param name="failureCode">The machine-readable code that names why the SD-JWT is refused, typically one of the
    /// <see cref="Verification.SdJwtVerificationFailureCodes"/>.</param>
    /// <param name="message">The human-readable description of the refusal.</param>
    public SdJwtException(string failureCode, string message)
        : base(message)
    {
        FailureCode = failureCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SdJwtException"/> class with the exception that caused it.
    /// </summary>
    /// <param name="failureCode">The machine-readable code that names why the SD-JWT is refused, typically one of the
    /// <see cref="Verification.SdJwtVerificationFailureCodes"/>.</param>
    /// <param name="message">The human-readable description of the refusal.</param>
    /// <param name="inner">The exception that caused the refusal.</param>
    public SdJwtException(string failureCode, string message, Exception inner)
        : base(message, inner)
    {
        FailureCode = failureCode;
    }

    /// <summary>
    /// Gets the machine-readable code that names why the SD-JWT is refused.
    /// </summary>
    public string FailureCode { get; }
}
