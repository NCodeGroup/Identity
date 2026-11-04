// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Verification;

namespace NCode.Identity.Jose.SdJwt.Tests;

public class DisclosureTests
{
    private static string EncodeJson(string json) =>
        Base64Url.Encode(SecureEncoding.UTF8.GetBytes(json));

    #region Parse Tests

    [Fact]
    public void Parse_WithSpecObjectPropertyDisclosure_ReadsSaltNameAndValue()
    {
        var disclosure = Disclosure.Parse(SpecVectors.FamilyNameDisclosure);

        Assert.Equal(SpecVectors.FamilyNameDisclosure, disclosure.Encoded);
        Assert.Equal(SpecVectors.FamilyNameSalt, disclosure.Salt);
        Assert.Equal("family_name", disclosure.ClaimName);
        Assert.False(disclosure.IsArrayElement);
        Assert.Equal("M\u00f6bius", disclosure.Value.GetString());
    }

    [Fact]
    public void Parse_WithSpecArrayElementDisclosure_ReadsSaltAndValue()
    {
        var disclosure = Disclosure.Parse(SpecVectors.NationalityDisclosure);

        Assert.Equal(SpecVectors.NationalitySalt, disclosure.Salt);
        Assert.Null(disclosure.ClaimName);
        Assert.True(disclosure.IsArrayElement);
        Assert.Equal("FR", disclosure.Value.GetString());
    }

    [Fact]
    public void Parse_WithNullValue_ReadsNull()
    {
        var encoded = EncodeJson("""["salt","claim",null]""");

        var disclosure = Disclosure.Parse(encoded);

        Assert.Equal(JsonValueKind.Null, disclosure.Value.ValueKind);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("not*base64url", "base64url")]
    [InlineData("eyJ", "base64url")]
    public void Parse_WhenNotBase64Url_Throws(string encoded, string reason)
    {
        var exception = Assert.Throws<SdJwtException>(() => Disclosure.Parse(encoded));

        Assert.Equal(SdJwtVerificationFailureCodes.InvalidDisclosure, exception.FailureCode);
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"salt":"x"}""", "JSON array")]
    [InlineData("""["salt"]""", "a salt, a claim name and a value")]
    [InlineData("""["salt","name","value","extra"]""", "a salt, a claim name and a value")]
    [InlineData("""[1,"name","value"]""", "salt of a disclosure must be a string")]
    [InlineData("""["","name","value"]""", "salt of a disclosure must not be empty")]
    [InlineData("""["salt",1,"value"]""", "claim name of a disclosure must be a string")]
    [InlineData("""["salt","","value"]""", "claim name of a disclosure must not be empty")]
    [InlineData("""["salt","_sd","value"]""", "reserved")]
    [InlineData("""["salt","...","value"]""", "reserved")]
    [InlineData("""["salt","_sd_alg","value"]""", "reserved")]
    [InlineData("""["salt","name","value"] trailing""", "JSON array")]
    public void Parse_WhenShapeIsWrong_Throws(string json, string reason)
    {
        var encoded = EncodeJson(json);

        var exception = Assert.Throws<SdJwtException>(() => Disclosure.Parse(encoded));

        Assert.Equal(SdJwtVerificationFailureCodes.InvalidDisclosure, exception.FailureCode);
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenNull_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Disclosure.Parse(null!));
    }

    #endregion

    #region TryParse Tests

    [Fact]
    public void TryParse_WhenWellFormed_ReturnsTrue()
    {
        var result = Disclosure.TryParse(
            SpecVectors.EmailDisclosure,
            out var disclosure,
            out var error
        );

        Assert.True(result);
        Assert.NotNull(disclosure);
        Assert.Null(error);
        Assert.Equal("email", disclosure.ClaimName);
        Assert.Equal("johndoe@example.com", disclosure.Value.GetString());
    }

    [Fact]
    public void TryParse_WhenMalformed_ReturnsFalseWithError()
    {
        var result = Disclosure.TryParse("!", out var disclosure, out var error);

        Assert.False(result);
        Assert.Null(disclosure);
        Assert.NotNull(error);
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_WithClaimName_RoundTrips()
    {
        var value = JsonSerializer.SerializeToElement(new { street = "Main" });

        var disclosure = Disclosure.Create("salt", "address", value);
        var parsed = Disclosure.Parse(disclosure.Encoded);

        Assert.Equal("salt", parsed.Salt);
        Assert.Equal("address", parsed.ClaimName);
        Assert.Equal("Main", parsed.Value.GetProperty("street").GetString());
        Assert.Equal(disclosure.Encoded, disclosure.ToString());
    }

    [Fact]
    public void Create_WithoutClaimName_RoundTripsAsArrayElement()
    {
        var value = JsonSerializer.SerializeToElement("DE");

        var disclosure = Disclosure.Create("salt", value);
        var parsed = Disclosure.Parse(disclosure.Encoded);

        Assert.True(parsed.IsArrayElement);
        Assert.Equal("DE", parsed.Value.GetString());
    }

    [Theory]
    [InlineData("_sd")]
    [InlineData("...")]
    [InlineData("_sd_alg")]
    public void Create_WithReservedClaimName_Throws(string claimName)
    {
        var value = JsonSerializer.SerializeToElement("x");

        Assert.Throws<ArgumentException>(() => Disclosure.Create("salt", claimName, value));
    }

    [Fact]
    public void Create_WithEmptySalt_Throws()
    {
        var value = JsonSerializer.SerializeToElement("x");

        Assert.Throws<ArgumentException>(() => Disclosure.Create("", "name", value));
        Assert.Throws<ArgumentException>(() => Disclosure.Create("", value));
    }

    #endregion

    #region ComputeDigest Tests

    [Theory]
    [InlineData(SpecVectors.FamilyNameDisclosure, SpecVectors.FamilyNameDigest)]
    [InlineData(SpecVectors.EmailDisclosure, SpecVectors.EmailDigest)]
    [InlineData(SpecVectors.NationalityDisclosure, SpecVectors.NationalityDigest)]
    public void ComputeDigest_WithSpecVector_MatchesSpecDigest(string encoded, string expected)
    {
        var disclosure = Disclosure.Parse(encoded);

        var digest = disclosure.ComputeDigest(SdJwtDigestAlgorithms.Sha256);

        Assert.Equal(expected, digest);
    }

    #endregion
}
