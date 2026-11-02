// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Jwk.Tests;

public class JsonWebKeyVocabularyTests
{
    [Theory]
    [InlineData(JsonWebKeyUses.Signature, "sig")]
    [InlineData(JsonWebKeyUses.Encryption, "enc")]
    public void Uses_HaveTheirRegisteredValues(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(JsonWebKeyOperations.Sign, "sign")]
    [InlineData(JsonWebKeyOperations.Verify, "verify")]
    [InlineData(JsonWebKeyOperations.Encrypt, "encrypt")]
    [InlineData(JsonWebKeyOperations.Decrypt, "decrypt")]
    [InlineData(JsonWebKeyOperations.WrapKey, "wrapKey")]
    [InlineData(JsonWebKeyOperations.UnwrapKey, "unwrapKey")]
    [InlineData(JsonWebKeyOperations.DeriveKey, "deriveKey")]
    [InlineData(JsonWebKeyOperations.DeriveBits, "deriveBits")]
    public void Operations_HaveTheirRegisteredValues(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(JsonWebKeyTypes.Rsa, "RSA")]
    [InlineData(JsonWebKeyTypes.Ec, "EC")]
    [InlineData(JsonWebKeyTypes.Okp, "OKP")]
    [InlineData(JsonWebKeyTypes.Oct, "oct")]
    [InlineData(JsonWebKeyTypes.Akp, "AKP")]
    public void KeyTypes_HaveTheirRegisteredValues(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(JsonWebKeySetParameterNames.Keys, "keys")]
    public void KeySetParameterNames_HaveTheirRegisteredValues(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(JsonWebKeyParameterNames.D, "d")]
    [InlineData(JsonWebKeyParameterNames.FirstPrimeFactor, "p")]
    [InlineData(JsonWebKeyParameterNames.SecondPrimeFactor, "q")]
    [InlineData(JsonWebKeyParameterNames.FirstFactorCrtExponent, "dp")]
    [InlineData(JsonWebKeyParameterNames.SecondFactorCrtExponent, "dq")]
    [InlineData(JsonWebKeyParameterNames.FirstCrtCoefficient, "qi")]
    [InlineData(JsonWebKeyParameterNames.OtherPrimesInfo, "oth")]
    public void PrivateKeyParameterNames_HaveTheirRegisteredValues(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }
}
