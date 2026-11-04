// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.SdJwt.Verification;

namespace NCode.Identity.Jose.SdJwt.Tests;

public class SdJwtSerializationTests
{
    private const string IssuerJwt = "eyJhbGciOiJFUzI1NiJ9.eyJfc2RfYWxnIjoic2hhLTI1NiJ9.c2ln";
    private const string KeyBindingJwt = "eyJhbGciOiJFUzI1NiIsInR5cCI6ImtiK2p3dCJ9.e30.a2I";

    #region Parse Tests

    [Fact]
    public void Parse_WithIssuanceFormat_ReadsDisclosuresAndNoKeyBinding()
    {
        var serialized =
            $"{IssuerJwt}~{SpecVectors.FamilyNameDisclosure}~{SpecVectors.NationalityDisclosure}~";

        var sdJwt = SdJwtSerialization.Parse(serialized, maxDisclosureCount: 10);

        Assert.Equal(IssuerJwt, sdJwt.IssuerSignedJwt);
        Assert.Equal(
            [SpecVectors.FamilyNameDisclosure, SpecVectors.NationalityDisclosure],
            sdJwt.Disclosures.Select(x => x.Encoded)
        );
        Assert.False(sdJwt.HasKeyBinding);
        Assert.Equal(serialized, sdJwt.ToString());
    }

    [Fact]
    public void Parse_WithKeyBinding_ReadsKeyBindingJwt()
    {
        var serialized = $"{IssuerJwt}~{SpecVectors.EmailDisclosure}~{KeyBindingJwt}";

        var sdJwt = SdJwtSerialization.Parse(serialized, maxDisclosureCount: 10);

        Assert.True(sdJwt.HasKeyBinding);
        Assert.Equal(KeyBindingJwt, sdJwt.KeyBindingJwt);
        Assert.Equal(serialized, sdJwt.ToString());
    }

    [Fact]
    public void Parse_WithNoDisclosures_ReadsIssuerJwtAlone()
    {
        var sdJwt = SdJwtSerialization.Parse($"{IssuerJwt}~", maxDisclosureCount: 0);

        Assert.Empty(sdJwt.Disclosures);
        Assert.False(sdJwt.HasKeyBinding);
    }

    [Theory]
    [InlineData(IssuerJwt)]
    [InlineData("~" + SpecVectors.EmailDisclosure + "~")]
    [InlineData("")]
    public void Parse_WhenNotCombinedFormat_Throws(string serialized)
    {
        var exception = Assert.Throws<SdJwtException>(() =>
            SdJwtSerialization.Parse(serialized, maxDisclosureCount: 10)
        );

        Assert.Equal(SdJwtVerificationFailureCodes.InvalidSdJwt, exception.FailureCode);
    }

    [Fact]
    public void Parse_WhenDisclosureIsEmpty_Throws()
    {
        var exception = Assert.Throws<SdJwtException>(() =>
            SdJwtSerialization.Parse($"{IssuerJwt}~~", maxDisclosureCount: 10)
        );

        Assert.Equal(SdJwtVerificationFailureCodes.InvalidDisclosure, exception.FailureCode);
    }

    [Fact]
    public void Parse_WhenOverTheDisclosureLimit_ThrowsBeforeDecoding()
    {
        // The second disclosure is malformed, so a parser that decoded before counting would report that instead.
        var serialized = $"{IssuerJwt}~{SpecVectors.EmailDisclosure}~!!!~";

        var exception = Assert.Throws<SdJwtException>(() =>
            SdJwtSerialization.Parse(serialized, maxDisclosureCount: 1)
        );

        Assert.Equal(SdJwtVerificationFailureCodes.DisclosureLimitExceeded, exception.FailureCode);
    }

    #endregion

    #region GetKeyBindingInput Tests

    [Fact]
    public void GetKeyBindingInput_WithKeyBinding_EndsAtLastSeparator()
    {
        var presentation = $"{IssuerJwt}~{SpecVectors.EmailDisclosure}~{KeyBindingJwt}";

        var input = SdJwtSerialization.GetKeyBindingInput(presentation).ToString();

        Assert.Equal($"{IssuerJwt}~{SpecVectors.EmailDisclosure}~", input);
    }

    #endregion

    #region SelectiveDisclosureJwt ToString Tests

    [Fact]
    public void ToString_WithoutKeyBinding_EndsWithSeparator()
    {
        var disclosure = Disclosure.Parse(SpecVectors.EmailDisclosure);
        var sdJwt = new SelectiveDisclosureJwt(IssuerJwt, [disclosure], keyBindingJwt: null);

        Assert.Equal($"{IssuerJwt}~{SpecVectors.EmailDisclosure}~", sdJwt.ToString());
    }

    #endregion
}
