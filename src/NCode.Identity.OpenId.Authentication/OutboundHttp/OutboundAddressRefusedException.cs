// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// The exception that is thrown when the outbound guard refused every address that a host resolves to.
/// </summary>
/// <param name="message">The message that describes the error.</param>
internal sealed class OutboundAddressRefusedException(string message)
    : HttpRequestException(HttpRequestError.ConnectionError, message, null);
