// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;
using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Represents one encoded CBOR data item, the value of a header parameter, a COSE_Key parameter or a claim. The value
/// is kept as its encoded bytes so that a reader decodes only the form it expects and a writer emits the item as given.
/// </summary>
[PublicAPI]
public sealed class CoseValue
{
    private CoseValue(ReadOnlyMemory<byte> encodedValue)
    {
        EncodedValue = encodedValue;
    }

    /// <summary>
    /// Gets the encoded CBOR data item.
    /// </summary>
    public ReadOnlyMemory<byte> EncodedValue { get; }

    /// <summary>
    /// Gets a value indicating whether the item is a map.
    /// </summary>
    public bool IsMap => TryPeek(out var state) && state == CborReaderState.StartMap;

    /// <summary>
    /// Creates a value from one encoded CBOR data item.
    /// </summary>
    /// <param name="encodedValue">The encoded item, exactly one well-formed CBOR data item.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentException"><paramref name="encodedValue"/> is not exactly one well-formed CBOR data
    /// item.</exception>
    public static CoseValue FromEncodedValue(ReadOnlyMemory<byte> encodedValue)
    {
        try
        {
            var reader = new CborReader(encodedValue, CborConformanceMode.Strict);
            reader.SkipValue();
            if (reader.BytesRemaining != 0)
                throw new ArgumentException(
                    "The encoded value holds more than one CBOR data item.",
                    nameof(encodedValue)
                );
        }
        catch (Exception exception)
            when (exception is CborContentException or InvalidOperationException)
        {
            throw new ArgumentException(
                "The encoded value is not a well-formed CBOR data item.",
                nameof(encodedValue),
                exception
            );
        }

        return new CoseValue(encodedValue.ToArray());
    }

    /// <summary>
    /// Creates an integer value.
    /// </summary>
    /// <param name="value">The integer.</param>
    /// <returns>The value.</returns>
    public static CoseValue FromInt64(long value)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteInt64(value);
        return new CoseValue(writer.Encode());
    }

    /// <summary>
    /// Creates a text string value.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    public static CoseValue FromTextString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteTextString(value);
        return new CoseValue(writer.Encode());
    }

    /// <summary>
    /// Creates a byte string value.
    /// </summary>
    /// <param name="value">The bytes.</param>
    /// <returns>The value.</returns>
    public static CoseValue FromByteString(ReadOnlySpan<byte> value)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteByteString(value);
        return new CoseValue(writer.Encode());
    }

    /// <summary>
    /// Creates a definite-length array of labels, the form of the <c>crit</c> header parameter.
    /// </summary>
    /// <param name="labels">The labels.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="labels"/> is <c>null</c>.</exception>
    public static CoseValue FromLabels(IReadOnlyCollection<CoseLabel> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartArray(labels.Count);
        foreach (var label in labels)
        {
            if (label.TextValue is { } text)
                writer.WriteTextString(text);
            else
                writer.WriteInt64(label.IntegerValue);
        }
        writer.WriteEndArray();
        return new CoseValue(writer.Encode());
    }

    /// <summary>
    /// Creates a definite-length array of text strings, such as the <c>aud</c> claim of a CBOR Web Token with several
    /// audiences.
    /// </summary>
    /// <param name="values">The text strings.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> contains a <c>null</c> element.</exception>
    public static CoseValue FromTextStrings(IReadOnlyCollection<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartArray(values.Count);
        foreach (var value in values)
        {
            if (value is null)
                throw new ArgumentException("A text string is null.", nameof(values));

            writer.WriteTextString(value);
        }
        writer.WriteEndArray();
        return new CoseValue(writer.Encode());
    }

    /// <summary>
    /// Reads the item as an integer that fits in a 64-bit signed integer.
    /// </summary>
    /// <param name="value">When this method returns <c>true</c>, the integer.</param>
    /// <returns><c>true</c> when the item is an untagged integer in range; otherwise, <c>false</c>.</returns>
    public bool TryGetInt64(out long value)
    {
        value = 0;
        try
        {
            var reader = NewReader();
            if (
                reader.PeekState()
                is not (CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger)
            )
                return false;

            value = reader.ReadInt64();
            return true;
        }
        catch (Exception exception)
            when (exception
                    is CborContentException
                        or InvalidOperationException
                        or OverflowException
            )
        {
            value = 0;
            return false;
        }
    }

    /// <summary>
    /// Reads the item as a definite-length text string.
    /// </summary>
    /// <param name="value">When this method returns <c>true</c>, the text.</param>
    /// <returns><c>true</c> when the item is an untagged text string; otherwise, <c>false</c>.</returns>
    public bool TryGetTextString([NotNullWhen(true)] out string? value)
    {
        value = null;
        try
        {
            var reader = NewReader();
            if (reader.PeekState() != CborReaderState.TextString)
                return false;

            value = reader.ReadTextString();
            return true;
        }
        catch (Exception exception)
            when (exception is CborContentException or InvalidOperationException)
        {
            value = null;
            return false;
        }
    }

    /// <summary>
    /// Reads the item as a definite-length byte string.
    /// </summary>
    /// <param name="value">When this method returns <c>true</c>, the bytes.</param>
    /// <returns><c>true</c> when the item is an untagged byte string; otherwise, <c>false</c>.</returns>
    public bool TryGetByteString(out ReadOnlyMemory<byte> value)
    {
        value = default;
        try
        {
            var reader = NewReader();
            if (reader.PeekState() != CborReaderState.ByteString)
                return false;

            value = reader.ReadByteString();
            return true;
        }
        catch (Exception exception)
            when (exception is CborContentException or InvalidOperationException)
        {
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Reads the item as a half, single or double precision floating-point number.
    /// </summary>
    /// <param name="value">When this method returns <c>true</c>, the number.</param>
    /// <returns><c>true</c> when the item is an untagged floating-point number; otherwise, <c>false</c>.</returns>
    public bool TryGetDouble(out double value)
    {
        value = 0;
        try
        {
            var reader = NewReader();
            if (
                reader.PeekState()
                is not (
                    CborReaderState.HalfPrecisionFloat
                    or CborReaderState.SinglePrecisionFloat
                    or CborReaderState.DoublePrecisionFloat
                )
            )
                return false;

            value = reader.ReadDouble();
            return true;
        }
        catch (Exception exception)
            when (exception is CborContentException or InvalidOperationException)
        {
            value = 0;
            return false;
        }
    }

    /// <summary>
    /// Reads the item as a definite-length array, each element kept as its own value.
    /// </summary>
    /// <param name="value">When this method returns <c>true</c>, the elements.</param>
    /// <returns><c>true</c> when the item is an untagged definite-length array; otherwise, <c>false</c>.</returns>
    public bool TryGetArray([NotNullWhen(true)] out IReadOnlyList<CoseValue>? value)
    {
        value = null;
        try
        {
            var reader = NewReader();
            if (reader.PeekState() != CborReaderState.StartArray)
                return false;

            var length = reader.ReadStartArray();
            if (length is null)
                return false;

            var elements = new List<CoseValue>(Math.Min(length.Value, reader.BytesRemaining));
            for (var index = 0; index < length; index++)
                elements.Add(new CoseValue(reader.ReadEncodedValue().ToArray()));

            reader.ReadEndArray();
            value = elements;
            return true;
        }
        catch (Exception exception)
            when (exception is CborContentException or InvalidOperationException)
        {
            value = null;
            return false;
        }
    }

    private CborReader NewReader() => new(EncodedValue, CborConformanceMode.Strict);

    private bool TryPeek(out CborReaderState state)
    {
        try
        {
            state = NewReader().PeekState();
            return true;
        }
        catch (Exception exception)
            when (exception is CborContentException or InvalidOperationException)
        {
            state = CborReaderState.Undefined;
            return false;
        }
    }
}
