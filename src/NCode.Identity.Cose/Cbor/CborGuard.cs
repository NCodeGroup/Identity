// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Cbor;
using NCode.Identity.Cose.Options;

namespace NCode.Identity.Cose.Cbor;

/// <summary>
/// Reads CBOR strictly and within the configured limits: no indefinite-length item, no trailing bytes, no duplicate
/// label, bounded size and bounded nesting. Every reader failure surfaces as a <see cref="CoseException"/> with fixed
/// text.
/// </summary>
internal static class CborGuard
{
    /// <summary>
    /// Determines whether the exception is one that the CBOR reader throws for malformed input.
    /// </summary>
    public static bool IsReaderException(Exception exception) =>
        exception
            is CborContentException
                or InvalidOperationException
                or OverflowException
                or FormatException
                or ArgumentException;

    /// <summary>
    /// Creates the exception for a malformed message.
    /// </summary>
    public static CoseException Invalid(string message, Exception? inner = null) =>
        inner is null
            ? new CoseException(CoseFailureCodes.InvalidMessage, message)
            : new CoseException(CoseFailureCodes.InvalidMessage, message, inner);

    /// <summary>
    /// Creates a strict reader over the bytes.
    /// </summary>
    public static CborReader NewReader(ReadOnlyMemory<byte> bytes) =>
        new(bytes, CborConformanceMode.Strict);

    /// <summary>
    /// Refuses input larger than the configured size limit before it is parsed.
    /// </summary>
    public static void CheckSize(ReadOnlyMemory<byte> bytes, CoseOptions options)
    {
        if (bytes.Length > options.MaxMessageSizeBytes)
            throw new CoseException(
                CoseFailureCodes.LimitExceeded,
                "The input is larger than the configured maximum size."
            );
    }

    /// <summary>
    /// Reads one data item, checks that it uses definite lengths only and stays within the nesting limit, and returns
    /// its encoded bytes.
    /// </summary>
    public static ReadOnlyMemory<byte> ReadValue(CborReader reader, int maxDepth)
    {
        try
        {
            var encoded = reader.ReadEncodedValue();
            CheckEncodedValue(encoded, maxDepth);
            return encoded;
        }
        catch (Exception exception) when (IsReaderException(exception))
        {
            throw Invalid("The CBOR data item is malformed.", exception);
        }
    }

    /// <summary>
    /// Checks one encoded data item for indefinite lengths and for nesting beyond the limit.
    /// </summary>
    public static void CheckEncodedValue(ReadOnlyMemory<byte> encoded, int maxDepth)
    {
        try
        {
            var reader = NewReader(encoded);
            Walk(reader, 0, maxDepth);
        }
        catch (Exception exception) when (IsReaderException(exception))
        {
            throw Invalid("The CBOR data item is malformed.", exception);
        }
    }

    /// <summary>
    /// Reads a definite-length map whose labels are integers or text strings, and whose values are bounded data items.
    /// The map itself is one level of nesting.
    /// </summary>
    public static IReadOnlyDictionary<CoseLabel, CoseValue> ReadMap(CborReader reader, int maxDepth)
    {
        try
        {
            if (maxDepth < 1)
                throw LimitExceeded();

            if (reader.PeekState() != CborReaderState.StartMap)
                throw Invalid("The CBOR data item is not a map.");

            var length =
                reader.ReadStartMap() ?? throw Invalid("The map uses an indefinite length.");

            var map = new Dictionary<CoseLabel, CoseValue>();
            for (var index = 0; index < length; index++)
            {
                var label = ReadLabel(reader);
                var value = CoseValue.FromEncodedValue(ReadValue(reader, maxDepth - 1));
                if (!map.TryAdd(label, value))
                    throw Invalid("The map repeats a label.");
            }

            reader.ReadEndMap();
            return map;
        }
        catch (Exception exception) when (IsReaderException(exception))
        {
            throw Invalid("The CBOR map is malformed.", exception);
        }
    }

    /// <summary>
    /// Reads a whole input that is exactly one map: the size limit applies first, and no byte may follow the map.
    /// </summary>
    public static IReadOnlyDictionary<CoseLabel, CoseValue> ReadRootMap(
        ReadOnlyMemory<byte> bytes,
        CoseOptions options
    )
    {
        CheckSize(bytes, options);

        try
        {
            var reader = NewReader(bytes);
            var map = ReadMap(reader, options.MaxNestingDepth);
            if (reader.BytesRemaining != 0)
                throw Invalid("The input has bytes after the map.");

            return map;
        }
        catch (Exception exception) when (IsReaderException(exception))
        {
            throw Invalid("The CBOR map is malformed.", exception);
        }
    }

    private static CoseLabel ReadLabel(CborReader reader)
    {
        switch (reader.PeekState())
        {
            case CborReaderState.UnsignedInteger:
            case CborReaderState.NegativeInteger:
                return new CoseLabel(reader.ReadInt64());
            case CborReaderState.TextString:
                return new CoseLabel(reader.ReadTextString());
            default:
                throw Invalid("A map label is neither an integer nor a text string.");
        }
    }

    private static CoseException LimitExceeded() =>
        new(CoseFailureCodes.LimitExceeded, "The CBOR data item is nested deeper than the limit.");

    private static void Walk(CborReader reader, int depth, int maxDepth)
    {
        switch (reader.PeekState())
        {
            case CborReaderState.StartArray:
            {
                var length =
                    reader.ReadStartArray()
                    ?? throw Invalid("The CBOR data item uses an indefinite length.");
                if (depth + 1 > maxDepth)
                    throw LimitExceeded();

                for (var index = 0; index < length; index++)
                    Walk(reader, depth + 1, maxDepth);

                reader.ReadEndArray();
                break;
            }
            case CborReaderState.StartMap:
            {
                var length =
                    reader.ReadStartMap()
                    ?? throw Invalid("The CBOR data item uses an indefinite length.");
                if (depth + 1 > maxDepth)
                    throw LimitExceeded();

                for (var index = 0; index < length * 2L; index++)
                    Walk(reader, depth + 1, maxDepth);

                reader.ReadEndMap();
                break;
            }
            case CborReaderState.Tag:
                reader.ReadTag();
                if (depth + 1 > maxDepth)
                    throw LimitExceeded();

                Walk(reader, depth + 1, maxDepth);
                break;
            case CborReaderState.StartIndefiniteLengthByteString:
            case CborReaderState.StartIndefiniteLengthTextString:
                throw Invalid("The CBOR data item uses an indefinite length.");
            default:
                reader.SkipValue();
                break;
        }
    }
}
