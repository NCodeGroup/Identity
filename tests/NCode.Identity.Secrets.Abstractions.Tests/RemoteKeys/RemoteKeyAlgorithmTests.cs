// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.RemoteKeys;

public class RemoteKeyAlgorithmTests
{
    private static RemoteSigningAlgorithm CreateSigningRow() =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RS256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            SigningAlgorithm = "RSASSA_PKCS1_V1_5_SHA_256",
        };

    private static RemoteDecryptionAlgorithm CreateDecryptionRow() =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RS256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

    private static RemoteKeyAgreementAlgorithm CreateKeyAgreementRow() =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RS256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            KeyAgreementAlgorithm = "ECDH",
        };

    #region Use Tests

    [Fact]
    public void Use_SigningRow_IsSig()
    {
        Assert.Equal(SecretKeyUses.Signature, CreateSigningRow().Use);
    }

    [Fact]
    public void Use_DecryptionRow_IsEnc()
    {
        Assert.Equal(SecretKeyUses.Encryption, CreateDecryptionRow().Use);
    }

    [Fact]
    public void Use_KeyAgreementRow_IsEnc()
    {
        Assert.Equal(SecretKeyUses.Encryption, CreateKeyAgreementRow().Use);
    }

    #endregion

    #region Selector Tests

    [Fact]
    public void Selector_DecryptionRow_NamesItsProviderAlgorithmTypeSizeAndCurve()
    {
        var row = new RemoteDecryptionAlgorithm
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RSA-OAEP-256",
            SecretType = "rsa",
            KeySizeBits = 3072,
            Curve = "none",
            KeySpec = "RSA_3072",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

        var selector = row.Selector;

        Assert.Equal(RemoteKeyProviderNames.AwsKms, selector.Provider);
        Assert.Equal("RSA-OAEP-256", selector.Algorithm);
        Assert.Equal("rsa", selector.SecretType);
        Assert.Equal(3072, selector.KeySizeBits);
        Assert.Equal("none", selector.Curve);
    }

    [Fact]
    public void Selector_RowsOfDifferentKindsWithTheSameValues_AreEqual()
    {
        RemoteKeyAlgorithm signing = CreateSigningRow();
        RemoteKeyAlgorithm decryption = CreateDecryptionRow();
        RemoteKeyAlgorithm keyAgreement = CreateKeyAgreementRow();

        Assert.Equal(signing.Selector, decryption.Selector);
        Assert.Equal(signing.Selector, keyAgreement.Selector);
    }

    #endregion

    #region Equals Tests

    [Fact]
    public void Equals_RowsOfDifferentKinds_AreNotEqual()
    {
        RemoteKeyAlgorithm signing = CreateSigningRow();
        RemoteKeyAlgorithm decryption = CreateDecryptionRow();
        RemoteKeyAlgorithm keyAgreement = CreateKeyAgreementRow();

        Assert.NotEqual(signing, decryption);
        Assert.NotEqual(signing, keyAgreement);
        Assert.NotEqual(decryption, keyAgreement);
    }

    #endregion
}
