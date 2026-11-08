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
