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

namespace NCode.Identity.OpenId.Authentication.Logic;

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
