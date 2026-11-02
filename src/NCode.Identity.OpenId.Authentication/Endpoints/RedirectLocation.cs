// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides the escaping of a redirect <c>Location</c> header value, which the authorization response and the
/// end-session response share.
/// </summary>
// See ADR-1203.
internal static class RedirectLocation
{
    /// <summary>
    /// Percent-encodes every character that a header value cannot carry as written (a non-ASCII character, RFC 3987
    /// §3.1, and an ASCII control character or space) as UTF-8, so a carriage return or line feed can never split the
    /// response; every other character is delivered as written.
    /// </summary>
    public static string Escape(string location)
    {
        if (location.AsSpan().IndexOfAnyExceptInRange('!', '~') < 0)
        {
            return location;
        }

        var builder = new StringBuilder(location.Length + 16);
        Span<byte> utf8 = stackalloc byte[4];

        foreach (var rune in location.EnumerateRunes())
        {
            if (rune.Value is > 0x20 and < 0x7F)
            {
                builder.Append((char)rune.Value);
                continue;
            }

            var count = rune.EncodeToUtf8(utf8);
            for (var i = 0; i < count; i++)
            {
                var hex = utf8[i].ToString("X2", CultureInfo.InvariantCulture);
                builder.Append('%').Append(hex);
            }
        }

        return builder.ToString();
    }
}
