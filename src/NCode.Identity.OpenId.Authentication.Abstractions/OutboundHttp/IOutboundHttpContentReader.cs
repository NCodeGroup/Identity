#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Buffers;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Reads the body of a server-initiated outbound HTTP response into a caller-provided <see cref="IBufferWriter{T}"/>
/// under a hard byte cap. Every fetch of a client-supplied URL (<c>jwks_uri</c>, <c>sector_identifier_uri</c>, and the
/// JAR <c>request_uri</c>) shares this replaceable seam so the oversized-response resource-exhaustion (DoS) defense is
/// uniform. Writing into a caller-owned (typically pooled) sink keeps the read free of an intermediate array and lets
/// the caller parse directly from the resulting <see cref="ReadOnlySequence{T}"/>.
/// </summary>
[PublicAPI]
public interface IOutboundHttpContentReader
{
    /// <summary>
    /// Copies the response body into <paramref name="destination"/>, stopping as soon as the body would exceed
    /// <paramref name="maxSizeBytes"/>. The advertised <c>Content-Length</c> is used only as an early reject; the cap is
    /// enforced while streaming so an unset or dishonest length cannot bypass it, and no bytes beyond the cap are
    /// written to the destination.
    /// </summary>
    /// <param name="response">The outbound HTTP response whose body is read.</param>
    /// <param name="destination">The sink the body is written into; the caller owns (and typically pools) it.</param>
    /// <param name="maxSizeBytes">The maximum number of body bytes to accept.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the read operation.
    /// </param>
    /// <returns>
    /// <see cref="OutboundHttpReadResult.Completed"/> when the whole body fit within the cap; otherwise
    /// <see cref="OutboundHttpReadResult.Exceeded"/>.
    /// </returns>
    ValueTask<OutboundHttpReadResult> ReadCappedAsync(
        HttpResponseMessage response,
        IBufferWriter<byte> destination,
        int maxSizeBytes,
        CancellationToken cancellationToken
    );
}
