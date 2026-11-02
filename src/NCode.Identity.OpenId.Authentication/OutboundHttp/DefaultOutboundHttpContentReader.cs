// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Provides the default implementation of <see cref="IOutboundHttpContentReader"/>. It streams the response body into
/// the caller's sink, enforcing the cap both via the advertised <c>Content-Length</c> (an early reject) and while
/// reading (the authoritative gate, so an unset or dishonest length cannot bypass it).
/// </summary>
internal sealed class DefaultOutboundHttpContentReader : IOutboundHttpContentReader
{
    /// <inheritdoc />
    public async ValueTask<OutboundHttpReadResult> ReadCappedAsync(
        HttpResponseMessage response,
        IBufferWriter<byte> destination,
        int maxSizeBytes,
        CancellationToken cancellationToken
    )
    {
        if (response.Content.Headers.ContentLength > maxSizeBytes)
            return OutboundHttpReadResult.Exceeded;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var total = 0;
        while (true)
        {
            var memory = destination.GetMemory();
            var read = await stream.ReadAsync(memory, cancellationToken);
            if (read == 0)
                break;

            // Enforce the cap before committing the chunk, so overflow bytes are never exposed to the caller.
            if (total + read > maxSizeBytes)
                return OutboundHttpReadResult.Exceeded;

            total += read;
            destination.Advance(read);
        }

        return OutboundHttpReadResult.Completed;
    }
}
