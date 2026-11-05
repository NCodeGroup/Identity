// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Buffers;
using NCode.Identity.Cose.Keys;

namespace NCode.Identity.Cose.Tests.Keys;

public class CoseKeyIdentifiersTests
{
    [Fact]
    public void GetKeyId_Utf8Text_ReturnsTheText()
    {
        Assert.Equal("11", CoseKeyIdentifiers.GetKeyId(CoseTestKeys.Hex("3131")));
    }

    [Theory]
    [InlineData("A1B2C3D4E5F60718", "h'a1b2c3d4e5f60718'")]
    [InlineData("DFD1AA976D8D4575A0FE34B96DE2BFAD", "h'dfd1aa976d8d4575a0fe34b96de2bfad'")]
    public void GetKeyId_BinaryBytes_ReturnsTheDiagnosticNotation(string hex, string expected)
    {
        Assert.Equal(expected, CoseKeyIdentifiers.GetKeyId(CoseTestKeys.Hex(hex)));
    }

    [Fact]
    public void GetKeyId_TextBeginningWithTheNotation_ReturnsTheDiagnosticNotation()
    {
        Assert.Equal("h'6827303027'", CoseKeyIdentifiers.GetKeyId(CoseTestKeys.Hex("6827303027")));
    }

    [Fact]
    public void GetBytes_DiagnosticNotation_ReturnsTheBytes()
    {
        Assert.Equal(
            CoseTestKeys.Hex("A1B2C3D4E5F60718"),
            CoseKeyIdentifiers.GetBytes("h'a1b2c3d4e5f60718'")
        );
    }

    [Theory]
    [InlineData("h'A1'")]
    [InlineData("h'a'")]
    [InlineData("h'6162'")]
    [InlineData("h''")]
    public void GetBytes_NonCanonicalNotation_ReturnsTheUtf8Text(string text)
    {
        Assert.Equal(SecureEncoding.UTF8.GetBytes(text), CoseKeyIdentifiers.GetBytes(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("3131")]
    [InlineData("FFFF")]
    [InlineData("A1B2C3D4E5F60718")]
    [InlineData("6827303027")]
    [InlineData("C3A9")]
    public void GetBytes_GetKeyId_RoundTripsEveryByteString(string hex)
    {
        var bytes = CoseTestKeys.Hex(hex);

        Assert.Equal(bytes, CoseKeyIdentifiers.GetBytes(CoseKeyIdentifiers.GetKeyId(bytes)));
    }
}
