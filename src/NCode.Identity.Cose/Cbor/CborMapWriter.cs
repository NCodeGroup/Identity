// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Cbor;

namespace NCode.Identity.Cose.Cbor;

/// <summary>
/// Writes COSE maps in the core deterministic encoding of RFC 8949 §4.2.1: definite lengths, the shortest integer
/// encodings, and map entries ordered by the bytewise lexicographic order of each label's own encoding.
/// </summary>
// See ADR-1409.
internal static class CborMapWriter
{
    /// <summary>
    /// Encodes one label.
    /// </summary>
    public static byte[] EncodeLabel(CoseLabel label)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        if (label.TextValue is { } text)
            writer.WriteTextString(text);
        else
            writer.WriteInt64(label.IntegerValue);

        return writer.Encode();
    }

    /// <summary>
    /// Writes a map with its entries in deterministic order.
    /// </summary>
    public static void WriteMap(CborWriter writer, IReadOnlyDictionary<CoseLabel, CoseValue> map)
    {
        var entries = new List<(byte[] Label, CoseValue Value)>(map.Count);
        foreach (var (label, value) in map)
            entries.Add((EncodeLabel(label), value));

        entries.Sort(static (left, right) => left.Label.AsSpan().SequenceCompareTo(right.Label));

        writer.WriteStartMap(entries.Count);
        foreach (var (label, value) in entries)
        {
            writer.WriteEncodedValue(label);
            writer.WriteEncodedValue(value.EncodedValue.Span);
        }

        writer.WriteEndMap();
    }

    /// <summary>
    /// Encodes a map.
    /// </summary>
    public static byte[] EncodeMap(IReadOnlyDictionary<CoseLabel, CoseValue> map)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        WriteMap(writer, map);
        return writer.Encode();
    }

    /// <summary>
    /// Encodes the content of a protected header bucket: the encoded map, or no bytes at all when the bucket holds no
    /// parameter.
    /// </summary>
    public static byte[] EncodeProtected(IReadOnlyDictionary<CoseLabel, CoseValue> map) =>
        map.Count == 0 ? [] : EncodeMap(map);

    /// <summary>
    /// Checks a value a caller supplies: definite lengths and nesting within the limit.
    /// </summary>
    /// <exception cref="ArgumentException">The value is refused.</exception>
    public static void ValidateValue(CoseValue value, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            CborGuard.CheckEncodedValue(value.EncodedValue, maxDepth);
        }
        catch (CoseException exception)
        {
            throw new ArgumentException(
                "A header value is not a definite-length CBOR item within the nesting limit.",
                nameof(value),
                exception
            );
        }
    }
}
