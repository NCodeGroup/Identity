// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// The exception that is thrown when the outbound guard refused every address that a host resolves to.
/// </summary>
internal sealed class OutboundAddressRefusedException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundAddressRefusedException"/> class.
    /// </summary>
    public OutboundAddressRefusedException()
        : base(HttpRequestError.ConnectionError, null, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundAddressRefusedException"/> class with the specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public OutboundAddressRefusedException(string message)
        : base(HttpRequestError.ConnectionError, message, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundAddressRefusedException"/> class with the specified
    /// error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public OutboundAddressRefusedException(string message, Exception innerException)
        : base(HttpRequestError.ConnectionError, message, innerException) { }
}
