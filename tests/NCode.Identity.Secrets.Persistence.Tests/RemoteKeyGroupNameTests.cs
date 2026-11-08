// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.RemoteKeys;

public class RemoteKeyCreationRequestTests
{
    private const string OwnerId = "tenant-1";

    private static RemoteSigningAlgorithm CreateAlgorithm(
        string provider = RemoteKeyProviderNames.AwsKms,
        string algorithm = "ES256",
        string keySpec = "ECC_NIST_P256",
        int keySizeBits = 256,
        string? curve = "P-256"
    ) =>
        new()
        {
            Provider = provider,
            Algorithm = algorithm,
            SecretType = "ecc",
            KeySizeBits = keySizeBits,
            Curve = curve,
            KeySpec = keySpec,
            SigningAlgorithm = "ECDSA_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
            IsDerEncodedSignature = true,
        };

    private static RemoteKeyCreationRequest CreateRequest(
        string ownerId = OwnerId,
        RemoteSigningAlgorithm? algorithm = null
    ) =>
        new()
        {
            OwnerId = ownerId,
            SecretId = "secret-1",
            Algorithm = algorithm ?? CreateAlgorithm(),
        };

    #region GroupName Property Tests

    [Fact]
    public void GroupName_Request_Has37CharactersAndNcidPrefix()
    {
        var groupName = CreateRequest().GroupName;

        Assert.Equal(37, groupName.Length);
        Assert.StartsWith("ncid-", groupName, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupName_Request_IsLowercaseHexAfterPrefix()
    {
        var groupName = CreateRequest().GroupName;

        Assert.All(groupName["ncid-".Length..], c => Assert.True(char.IsAsciiHexDigitLower(c)));
    }

    [Fact]
    public void GroupName_EqualInputs_IsStable()
    {
        var first = CreateRequest().GroupName;
        var second = CreateRequest().GroupName;

        Assert.Equal(first, second);
    }

    [Fact]
    public void GroupName_DifferentSecretId_IsEqual()
    {
        var first = CreateRequest();
        var second = new RemoteKeyCreationRequest
        {
            OwnerId = OwnerId,
            SecretId = "secret-2",
            Algorithm = CreateAlgorithm(),
        };

        Assert.Equal(first.GroupName, second.GroupName);
    }

    [Fact]
    public void GroupName_DifferentOwner_Differs()
    {
        var baseline = CreateRequest().GroupName;

        var actual = CreateRequest(ownerId: "tenant-2").GroupName;

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void GroupName_DifferentProvider_Differs()
    {
        var baseline = CreateRequest().GroupName;

        var actual = CreateRequest(
            algorithm: CreateAlgorithm(provider: RemoteKeyProviderNames.AzureKeyVault)
        ).GroupName;

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void GroupName_DifferentAlgorithm_Differs()
    {
        var baseline = CreateRequest().GroupName;

        var actual = CreateRequest(algorithm: CreateAlgorithm(algorithm: "ES256K")).GroupName;

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void GroupName_DifferentKeySpec_Differs()
    {
        var baseline = CreateRequest().GroupName;

        var actual = CreateRequest(
            algorithm: CreateAlgorithm(keySpec: "ECC_SECG_P256K1")
        ).GroupName;

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void GroupName_DifferentKeySize_Differs()
    {
        var baseline = CreateRequest().GroupName;

        var actual = CreateRequest(algorithm: CreateAlgorithm(keySizeBits: 384)).GroupName;

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void GroupName_DifferentCurve_Differs()
    {
        var baseline = CreateRequest().GroupName;

        var actual = CreateRequest(algorithm: CreateAlgorithm(curve: "secp256k1")).GroupName;

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void GroupName_NullCurve_Differs()
    {
        var baseline = CreateRequest().GroupName;

        var actual = CreateRequest(algorithm: CreateAlgorithm(curve: null)).GroupName;

        Assert.NotEqual(baseline, actual);
        Assert.Equal(37, actual.Length);
    }

    #endregion
}
