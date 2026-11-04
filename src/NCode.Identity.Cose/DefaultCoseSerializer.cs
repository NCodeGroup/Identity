// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Messages;
using NCode.Identity.Cose.Options;

namespace NCode.Identity.Cose;

/// <summary>
/// Provides the default implementation of <see cref="ICoseSerializer"/>.
/// </summary>
// See ADR-1409.
internal class DefaultCoseSerializer(IOptions<CoseOptions> options) : ICoseSerializer
{
    private CoseOptions Options { get; } = options.Value;

    /// <inheritdoc />
    public CoseMessageInfo Inspect(
        ReadOnlyMemory<byte> message,
        CoseMessageType? untaggedMessageType
    )
    {
        var parsed = CoseMessageReader.Read(message, untaggedMessageType, Options);
        return new CoseMessageInfo
        {
            MessageType = parsed.MessageType,
            IsTagged = parsed.IsTagged,
            IsContentDetached = parsed.Content is null,
            ProtectedHeaders = parsed.Body.Protected,
            UnprotectedHeaders = parsed.Body.Unprotected,
            Signatures = [.. parsed.Signatures.Select(ToHeaders)],
            Recipients = [.. parsed.Recipients.Select(ToHeaders)],
        };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<CoseLabel, CoseValue> DecodeMap(ReadOnlyMemory<byte> encodedMap) =>
        CborGuard.ReadRootMap(encodedMap, Options);

    /// <inheritdoc />
    public byte[] EncodeMap(IReadOnlyDictionary<CoseLabel, CoseValue> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        foreach (var value in map.Values)
            CborMapWriter.ValidateValue(value, Options.MaxNestingDepth - 1);

        return CborMapWriter.EncodeMap(map);
    }

    private static CoseLayerHeaders ToHeaders(ParsedCoseLayer layer) =>
        new() { ProtectedHeaders = layer.Protected, UnprotectedHeaders = layer.Unprotected };
}
