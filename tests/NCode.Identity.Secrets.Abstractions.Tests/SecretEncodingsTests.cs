// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets;

public class SecretEncodingsTests
{
    #region IsPublic Tests

    [Fact]
    public void IsPublic_Spki_ReturnsTrue()
    {
        Assert.True(SecretEncodings.IsPublic(SecretEncodings.Spki));
    }

    [Fact]
    public void IsPublic_X509_ReturnsTrue()
    {
        Assert.True(SecretEncodings.IsPublic(SecretEncodings.X509));
    }

    [Fact]
    public void IsPublic_Pkcs8_ReturnsFalse()
    {
        Assert.False(SecretEncodings.IsPublic(SecretEncodings.Pkcs8));
    }

    [Fact]
    public void IsPublic_SymmetricRaw_ReturnsFalse()
    {
        Assert.False(SecretEncodings.IsPublic(SecretEncodings.SymmetricRaw));
    }

    [Fact]
    public void IsPublic_Unknown_ReturnsFalse()
    {
        Assert.False(SecretEncodings.IsPublic("unknown"));
    }

    [Fact]
    public void IsPublic_DifferentCase_ReturnsFalse()
    {
        Assert.False(SecretEncodings.IsPublic("SPKI"));
        Assert.False(SecretEncodings.IsPublic("X509"));
    }

    [Fact]
    public void IsPublic_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SecretEncodings.IsPublic(null!));
    }

    #endregion

    #region IsVerifier Tests

    [Fact]
    public void IsPublic_Verifier_ReturnsFalse()
    {
        // A verifier is not public: it is stored encrypted like every other non-public encoding.
        Assert.False(SecretEncodings.IsPublic(SecretEncodings.Verifier));
    }

    [Fact]
    public void IsVerifier_Verifier_ReturnsTrue()
    {
        Assert.True(SecretEncodings.IsVerifier(SecretEncodings.Verifier));
    }

    [Theory]
    [InlineData(SecretEncodings.SymmetricRaw)]
    [InlineData(SecretEncodings.Pkcs8)]
    [InlineData(SecretEncodings.Spki)]
    [InlineData("VERIFIER")]
    public void IsVerifier_AnotherEncoding_ReturnsFalse(string secretEncoding)
    {
        Assert.False(SecretEncodings.IsVerifier(secretEncoding));
    }

    [Fact]
    public void IsVerifier_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SecretEncodings.IsVerifier(null!));
    }

    #endregion
}
