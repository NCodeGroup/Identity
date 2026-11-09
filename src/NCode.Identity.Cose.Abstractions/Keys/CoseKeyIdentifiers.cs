// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Maps a COSE key identifier, an opaque byte string (RFC 9052 §3.1, §7.1), onto the text <c>KeyId</c> of a key and
/// back. Bytes that are UTF-8 text and do not begin with <c>h'</c> are named by that text, so a key shared with JOSE
/// keeps one identifier; any other bytes are named in CBOR diagnostic notation (RFC 8949 §8), <c>h'</c> followed by
/// lower-case hexadecimal digits and <c>'</c>, such as <c>h'a1b2c3d4e5f60718'</c>. A name in that notation stands for
/// the bytes it spells and any other name for its UTF-8 text, so <c>GetBytes(GetKeyId(bytes))</c> returns
/// <c>bytes</c> for every byte string and two different identifiers never share a name.
/// </summary>
// See ADR-1409.
[PublicAPI]
public static class CoseKeyIdentifiers
{
    private const string NotationPrefix = "h'";
    private const char NotationSuffix = '\'';

    private static UTF8Encoding StrictUtf8 { get; } =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Names a COSE key identifier as the text <c>KeyId</c> of a key.
    /// </summary>
    /// <param name="keyId">The bytes of the COSE key identifier.</param>
    /// <returns>The UTF-8 text of the bytes, or their CBOR diagnostic notation when they are not such text.</returns>
    public static string GetKeyId(ReadOnlySpan<byte> keyId)
    {
        try
        {
            var text = StrictUtf8.GetString(keyId);
            if (!text.StartsWith(NotationPrefix, StringComparison.Ordinal))
                return text;
        }
        catch (DecoderFallbackException)
        {
            // Not text: the bytes are named in diagnostic notation.
        }

        return NotationPrefix + Convert.ToHexStringLower(keyId) + NotationSuffix;
    }

    /// <summary>
    /// Gets the bytes of the COSE key identifier that a key's text <c>KeyId</c> stands for.
    /// </summary>
    /// <param name="keyId">The <c>KeyId</c> of a key.</param>
    /// <returns>
    /// The bytes that the name spells when it is in CBOR diagnostic notation as <see cref="GetKeyId"/> writes it,
    /// otherwise the UTF-8 encoding of the name.
    /// </returns>
    /// <exception cref="ArgumentException">The name is not valid Unicode text.</exception>
    public static byte[] GetBytes(string keyId)
    {
        ArgumentNullException.ThrowIfNull(keyId);

        if (
            keyId.Length >= NotationPrefix.Length + 1
            && keyId.StartsWith(NotationPrefix, StringComparison.Ordinal)
            && keyId[^1] == NotationSuffix
        )
        {
            var digits = keyId.AsSpan(
                NotationPrefix.Length,
                keyId.Length - NotationPrefix.Length - 1
            );
            if (digits.Length % 2 == 0 && IsLowerHex(digits))
            {
                var decoded = Convert.FromHexString(digits);
                if (string.Equals(GetKeyId(decoded), keyId, StringComparison.Ordinal))
                    return decoded;
            }
        }

        return StrictUtf8.GetBytes(keyId);
    }

    private static bool IsLowerHex(ReadOnlySpan<char> digits)
    {
        foreach (var digit in digits)
        {
            if (digit is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
        }

        return true;
    }
}
