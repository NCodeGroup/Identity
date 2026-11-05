// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Cbor;

namespace NCode.Identity.Cose.Tests;

/// <summary>
/// Reads parts of an encoded message for assertions.
/// </summary>
public static class CoseTestCbor
{
    /// <summary>
    /// Reads the content, or the ciphertext, of a tagged message: the third element of its array.
    /// </summary>
    public static byte[] ReadContent(byte[] taggedMessage)
    {
        var reader = new CborReader(taggedMessage, CborConformanceMode.Strict);
        reader.ReadTag();
        reader.ReadStartArray();
        reader.SkipValue();
        reader.SkipValue();
        return reader.ReadByteString();
    }
}
