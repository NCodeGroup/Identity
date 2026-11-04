// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Buffers.Text;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Encodes the identifier of the last row of a page into an opaque continuation cursor and decodes it again, for the
/// stores that page by keyset without deriving from the base store.
/// </summary>
internal static class KeysetCursor
{
    /// <summary>
    /// Encodes a keyset sort key into an opaque, base64url continuation cursor.
    /// </summary>
    /// <param name="key">The sort key of the last item on the page.</param>
    /// <returns>The opaque continuation cursor.</returns>
    public static string Encode(long key)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, key);
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>
    /// Decodes an opaque continuation cursor into its keyset sort key, treating a malformed cursor as the start of
    /// the collection rather than a hard error.
    /// </summary>
    /// <param name="cursor">The opaque cursor, or <c>null</c> to start at the beginning.</param>
    /// <returns>The exclusive lower-bound sort key, or <c>null</c> to start at the beginning.</returns>
    public static long? Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
            return null;

        Span<byte> bytes = stackalloc byte[sizeof(long)];
        try
        {
            if (
                !Base64Url.TryDecodeFromChars(cursor, bytes, out var written)
                || written != sizeof(long)
            )
                return null;
        }
        catch (FormatException)
        {
            return null;
        }

        return BinaryPrimitives.ReadInt64BigEndian(bytes);
    }
}
