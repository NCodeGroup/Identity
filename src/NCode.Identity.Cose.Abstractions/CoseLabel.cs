// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Represents a label of a CBOR Object Signing and Encryption (COSE) map: an integer or a text string. A label keys
/// the header buckets of a COSE message, a COSE_Key and the claims set of a CBOR Web Token (CWT).
/// </summary>
[PublicAPI]
public readonly record struct CoseLabel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoseLabel"/> structure with an integer label.
    /// </summary>
    /// <param name="value">The integer label.</param>
    public CoseLabel(long value)
    {
        IntegerValue = value;
        TextValue = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CoseLabel"/> structure with a text label.
    /// </summary>
    /// <param name="value">The text label.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    public CoseLabel(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        IntegerValue = 0;
        TextValue = value;
    }

    /// <summary>
    /// Gets the integer label, or zero when the label is a text string.
    /// </summary>
    public long IntegerValue { get; }

    /// <summary>
    /// Gets the text label, or <c>null</c> when the label is an integer.
    /// </summary>
    public string? TextValue { get; }

    /// <summary>
    /// Gets a value indicating whether the label is a text string.
    /// </summary>
    public bool IsText => TextValue is not null;

    /// <summary>
    /// Converts an integer to an integer label.
    /// </summary>
    /// <param name="value">The integer label.</param>
    public static implicit operator CoseLabel(long value) => new(value);

    /// <summary>
    /// Converts a string to a text label.
    /// </summary>
    /// <param name="value">The text label.</param>
    public static implicit operator CoseLabel(string value) => new(value);

    /// <summary>
    /// Returns the integer label in invariant culture, or the text label.
    /// </summary>
    public override string ToString() =>
        TextValue ?? IntegerValue.ToString(CultureInfo.InvariantCulture);
}
