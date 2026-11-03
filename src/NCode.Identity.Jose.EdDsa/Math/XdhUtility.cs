// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.EdDsa.Math;

/// <summary>
/// Helpers shared by the <see cref="X25519"/> and <see cref="X448"/> Diffie-Hellman functions.
/// </summary>
internal static class XdhUtility
{
    /// <summary>
    /// Determines whether any byte of a shared secret is non-zero. Every byte is read whatever its value, so the time
    /// taken reveals only the answer, never where a non-zero byte is (RFC 7748 §6.1 asks for the check to be
    /// constant-time).
    /// </summary>
    public static bool IsNonZero(ReadOnlySpan<byte> value)
    {
        var accumulator = 0;
        foreach (var octet in value)
        {
            accumulator |= octet;
        }

        return accumulator != 0;
    }
}
