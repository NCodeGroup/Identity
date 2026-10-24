#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.OpenId.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class DistinguishedNameTests
{
    private const string CommonNameOid = "2.5.4.3";
    private const string OrganizationOid = "2.5.4.10";
    private const string OrganizationalUnitOid = "2.5.4.11";
    private const string StateOid = "2.5.4.8";
    private const string OrganizationIdentifierOid = "2.5.4.97";

    private static X500DistinguishedName CreateName(
        params (string Oid, string Value, UniversalTagNumber Encoding)[] attributes
    )
    {
        var builder = new X500DistinguishedNameBuilder();
        foreach (var (oid, value, encoding) in attributes)
        {
            builder.Add(oid, value, encoding);
        }

        return builder.Build();
    }

    private static X500DistinguishedName CreateMultiValuedName(
        params (string Oid, string Value)[] attributes
    )
    {
        // BER keeps the order of the SET OF, unlike DER which sorts it
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            using (writer.PushSetOf())
            {
                foreach (var (oid, value) in attributes)
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(oid);
                        writer.WriteCharacterString(UniversalTagNumber.UTF8String, value);
                    }
                }
            }
        }

        return new X500DistinguishedName(writer.Encode());
    }

    private static void AssertParsesToMatch(string value, X500DistinguishedName expected)
    {
        // The builder encodes the last attribute added first, so the expected name is built in the order of the string.
        Assert.True(DistinguishedName.TryParse(value, out var parsed));
        Assert.NotNull(parsed);

        var expectedName = DistinguishedName.FromX500(expected);
        Assert.True(parsed.Matches(expectedName));
        Assert.True(expectedName.Matches(parsed));
    }

    #region Matches Tests

    [Fact]
    public void Matches_WhenOnlyTheStringEncodingDiffers_ReturnsTrue()
    {
        var utf8 = DistinguishedName.FromX500(
            CreateName((OrganizationOid, "Example Org", UniversalTagNumber.UTF8String))
        );
        var printable = DistinguishedName.FromX500(
            CreateName((OrganizationOid, "Example Org", UniversalTagNumber.PrintableString))
        );

        Assert.True(utf8.Matches(printable));
        Assert.True(printable.Matches(utf8));
    }

    [Fact]
    public void Matches_WhenOnlyCaseAndWhitespaceDiffer_ReturnsTrue()
    {
        var left = DistinguishedName.FromX500(
            CreateName((OrganizationOid, "Example  Org ", UniversalTagNumber.UTF8String))
        );
        var right = DistinguishedName.FromX500(
            CreateName((OrganizationOid, "example org", UniversalTagNumber.UTF8String))
        );

        Assert.True(left.Matches(right));
        Assert.True(right.Matches(left));
    }

    [Fact]
    public void Matches_WhenTheOrderDiffers_ReturnsFalse()
    {
        var left = DistinguishedName.FromX500(
            CreateName(
                (CommonNameOid, "client", UniversalTagNumber.UTF8String),
                (OrganizationOid, "Example", UniversalTagNumber.UTF8String)
            )
        );
        var right = DistinguishedName.FromX500(
            CreateName(
                (OrganizationOid, "Example", UniversalTagNumber.UTF8String),
                (CommonNameOid, "client", UniversalTagNumber.UTF8String)
            )
        );

        Assert.False(left.Matches(right));
        Assert.False(right.Matches(left));
    }

    [Fact]
    public void Matches_WhenAValueDiffers_ReturnsFalse()
    {
        var left = DistinguishedName.FromX500(
            CreateName((OrganizationOid, "Example", UniversalTagNumber.UTF8String))
        );
        var right = DistinguishedName.FromX500(
            CreateName((OrganizationOid, "Other", UniversalTagNumber.UTF8String))
        );

        Assert.False(left.Matches(right));
        Assert.False(right.Matches(left));
    }

    [Fact]
    public void Matches_WhenTheCountDiffers_ReturnsFalse()
    {
        var left = DistinguishedName.FromX500(
            CreateName(
                (CommonNameOid, "client", UniversalTagNumber.UTF8String),
                (OrganizationOid, "Example", UniversalTagNumber.UTF8String)
            )
        );
        var right = DistinguishedName.FromX500(
            CreateName((CommonNameOid, "client", UniversalTagNumber.UTF8String))
        );

        Assert.False(left.Matches(right));
        Assert.False(right.Matches(left));
    }

    [Fact]
    public void Matches_WhenAMultiValuedRdnListsItsValuesInAnotherOrder_ReturnsTrue()
    {
        var left = DistinguishedName.FromX500(
            CreateMultiValuedName((CommonNameOid, "client"), (OrganizationalUnitOid, "Unit"))
        );
        var right = DistinguishedName.FromX500(
            CreateMultiValuedName((OrganizationalUnitOid, "Unit"), (CommonNameOid, "client"))
        );

        Assert.True(left.Matches(right));
        Assert.True(right.Matches(left));
    }

    #endregion

    #region TryParse Tests

    [Fact]
    public void TryParse_WhenTheNameUsesKeywords_ReturnsTheNameMostSignificantFirst()
    {
        AssertParsesToMatch(
            "CN=client,O=Example",
            CreateName(
                (CommonNameOid, "client", UniversalTagNumber.UTF8String),
                (OrganizationOid, "Example", UniversalTagNumber.UTF8String)
            )
        );
    }

    [Fact]
    public void TryParse_WhenWhitespaceSurroundsTheSeparators_ReturnsTheName()
    {
        AssertParsesToMatch(
            " cn = client ,  o=Example ",
            CreateName(
                (CommonNameOid, "client", UniversalTagNumber.UTF8String),
                (OrganizationOid, "Example", UniversalTagNumber.UTF8String)
            )
        );
    }

    [Fact]
    public void TryParse_WhenTheKeywordIsAnAlias_ReturnsTheName()
    {
        AssertParsesToMatch(
            "CN=client, S=Ontario",
            CreateName(
                (CommonNameOid, "client", UniversalTagNumber.UTF8String),
                (StateOid, "Ontario", UniversalTagNumber.UTF8String)
            )
        );
    }

    [Theory]
    [InlineData("OID.2.5.4.97=PSDGB-FCA-123456")]
    [InlineData("2.5.4.97=PSDGB-FCA-123456")]
    public void TryParse_WhenTheTypeIsADottedOid_ReturnsTheName(string value)
    {
        AssertParsesToMatch(
            value,
            CreateName(
                (OrganizationIdentifierOid, "PSDGB-FCA-123456", UniversalTagNumber.UTF8String)
            )
        );
    }

    [Theory]
    [InlineData(@"CN=Doe\, John", "Doe, John")]
    [InlineData("CN=\"Doe, John\"", "Doe, John")]
    [InlineData(@"CN=caf\C3\A9", "caf\u00E9")]
    [InlineData("CN=#0C06636C69656E74", "client")]
    public void TryParse_WhenTheValueIsEscapedQuotedOrHex_ReturnsTheName(
        string value,
        string expectedValue
    )
    {
        AssertParsesToMatch(
            value,
            CreateName((CommonNameOid, expectedValue, UniversalTagNumber.UTF8String))
        );
    }

    [Fact]
    public void TryParse_WhenARelativeNameIsMultiValued_ReturnsTheValuesAsASet()
    {
        Assert.True(DistinguishedName.TryParse("CN=a+UID=b,O=x", out var left));
        Assert.True(DistinguishedName.TryParse("UID=b + CN=a, O=x", out var right));
        Assert.NotNull(left);
        Assert.NotNull(right);

        Assert.True(left.Matches(right));
        Assert.True(right.Matches(left));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CN")]
    [InlineData("=x")]
    [InlineData("XX=y")]
    [InlineData("CN=a,")]
    [InlineData("CN=a,,O=b")]
    [InlineData("CN=a\"b")]
    [InlineData(@"CN=\ZZ")]
    [InlineData(@"CN=\C3")]
    [InlineData("CN=#0C")]
    [InlineData("/C=US/O=Example")]
    [InlineData("CN=a;O=b")]
    public void TryParse_WhenTheStringIsNotAName_ReturnsFalse(string value)
    {
        Assert.False(DistinguishedName.TryParse(value, out var parsed));
        Assert.Null(parsed);
    }

    #endregion

    #region FromX500 Tests

    [Fact]
    public void FromX500_WhenTheNameIsMalformed_ThrowsCryptographicException()
    {
        Assert.ThrowsAny<CryptographicException>(() =>
            DistinguishedName.FromX500(
                new X500DistinguishedName(new byte[] { 0x30, 0x03, 0x31, 0x01, 0x00 })
            )
        );
    }

    #endregion
}
