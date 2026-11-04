// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Messages;
using NCode.Identity.Cose.Options;
using NCode.Identity.Cose.Randomness;
using NCode.Identity.Secrets.KeyMaterial;

namespace NCode.Identity.Cose;

/// <summary>
/// Provides the default implementation of <see cref="ICoseSerializer"/>.
/// </summary>
// See ADR-1409.
internal partial class DefaultCoseSerializer(
    ICoseAlgorithmCatalog algorithmCatalog,
    ISecretKeyFactory secretKeyFactory,
    ICoseRandomSource randomSource,
    IOptions<CoseOptions> options,
    TimeProvider timeProvider
) : ICoseSerializer
{
    private ICoseAlgorithmCatalog AlgorithmCatalog { get; } = algorithmCatalog;

    private ISecretKeyFactory SecretKeyFactory { get; } = secretKeyFactory;

    private ICoseRandomSource RandomSource { get; } = randomSource;

    private CoseOptions Options { get; } = options.Value;

    private TimeProvider TimeProvider { get; } = timeProvider;

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
            Signatures = ToHeaders(parsed.Signatures),
            Recipients = ToHeaders(parsed.Recipients),
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

    private static CoseLayerHeaders[] ToHeaders(IReadOnlyList<ParsedCoseLayer> layers)
    {
        if (layers.Count == 0)
            return [];

        var headers = new CoseLayerHeaders[layers.Count];
        for (var index = 0; index < headers.Length; index++)
        {
            var layer = layers[index];
            headers[index] = new CoseLayerHeaders
            {
                ProtectedHeaders = layer.Protected,
                UnprotectedHeaders = layer.Unprotected,
            };
        }

        return headers;
    }
}
