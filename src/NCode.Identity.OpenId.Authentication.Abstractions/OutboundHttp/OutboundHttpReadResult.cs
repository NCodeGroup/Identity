// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// The outcome of <see cref="IOutboundHttpContentReader.ReadCappedAsync"/>.
/// </summary>
[PublicAPI]
public enum OutboundHttpReadResult
{
    /// <summary>
    /// The entire response body was read into the destination and fit within the configured size cap.
    /// </summary>
    Completed = 0,

    /// <summary>
    /// The response body exceeded the configured size cap; the read was abandoned and the destination holds only a
    /// prefix of the body.
    /// </summary>
    Exceeded = 1,
}
