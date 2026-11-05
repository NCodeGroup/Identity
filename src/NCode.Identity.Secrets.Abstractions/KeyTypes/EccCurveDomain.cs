// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Describes the domain of a short-Weierstrass elliptic curve <c>y² = x³ + ax + b</c> over the prime field of order
/// <see cref="Prime"/>, and checks whether a point lies on it independently of the platform's cryptography provider.
/// </summary>
// See ADR-1427.
[PublicAPI]
public sealed class EccCurveDomain
{
    /// <summary>
    /// Creates a curve domain from big-endian unsigned hexadecimal values.
    /// </summary>
    /// <param name="prime">The prime <c>p</c> of the field, in hexadecimal.</param>
    /// <param name="a">The coefficient <c>a</c> of the curve equation, in hexadecimal.</param>
    /// <param name="b">The coefficient <c>b</c> of the curve equation, in hexadecimal.</param>
    /// <returns>The curve domain.</returns>
    public static EccCurveDomain FromHex(string prime, string a, string b)
    {
        return new EccCurveDomain
        {
            Prime = ParseHex(prime),
            A = ParseHex(a),
            B = ParseHex(b),
        };
    }

    private static BigInteger ParseHex(string value)
    {
        var bytes = Convert.FromHexString(value);
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    /// <summary>
    /// Gets the prime <c>p</c> of the field.
    /// </summary>
    public required BigInteger Prime { get; init; }

    /// <summary>
    /// Gets the coefficient <c>a</c> of the curve equation <c>y² = x³ + ax + b</c>.
    /// </summary>
    public required BigInteger A { get; init; }

    /// <summary>
    /// Gets the coefficient <c>b</c> of the curve equation <c>y² = x³ + ax + b</c>.
    /// </summary>
    public required BigInteger B { get; init; }

    /// <summary>
    /// Gets the length, in bytes, of a field element and therefore of each point coordinate.
    /// </summary>
    public int FieldSizeBytes => (int)((Prime.GetBitLength() + 7) / 8);

    /// <summary>
    /// Determines whether a point lies on the curve.
    /// </summary>
    /// <param name="x">The big-endian unsigned <c>x</c> coordinate, exactly <see cref="FieldSizeBytes"/> long.</param>
    /// <param name="y">The big-endian unsigned <c>y</c> coordinate, exactly <see cref="FieldSizeBytes"/> long.</param>
    /// <returns>
    /// <see langword="true"/> when both coordinates are <see cref="FieldSizeBytes"/> long, are less than
    /// <see cref="Prime"/>, and satisfy the curve equation; otherwise, <see langword="false"/>.
    /// </returns>
    public bool ContainsPoint(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        var fieldSize = FieldSizeBytes;
        if (x.Length != fieldSize || y.Length != fieldSize)
        {
            return false;
        }

        var xValue = new BigInteger(x, isUnsigned: true, isBigEndian: true);
        var yValue = new BigInteger(y, isUnsigned: true, isBigEndian: true);
        if (xValue >= Prime || yValue >= Prime)
        {
            return false;
        }

        var difference = (yValue * yValue) - ((xValue * xValue * xValue) + (A * xValue) + B);
        var remainder = BigInteger.Remainder(difference, Prime);
        if (remainder.Sign < 0)
        {
            remainder += Prime;
        }

        return remainder.IsZero;
    }
}
