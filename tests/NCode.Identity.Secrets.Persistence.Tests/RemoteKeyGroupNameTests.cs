// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Persistence;

public class RemoteKeyGroupNameTests
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

    private static string CreateGroupName(
        string ownerId = OwnerId,
        RemoteSigningAlgorithm? algorithm = null
    ) => RemoteKeyGroupName.Create(ownerId, algorithm ?? CreateAlgorithm());

    #region Create Tests

    [Fact]
    public void Create_Request_Has37CharactersAndNcidPrefix()
    {
        var groupName = CreateGroupName();

        Assert.Equal(37, groupName.Length);
        Assert.StartsWith("ncid-", groupName, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_Request_IsLowercaseHexAfterPrefix()
    {
        var groupName = CreateGroupName();

        Assert.All(groupName["ncid-".Length..], c => Assert.True(char.IsAsciiHexDigitLower(c)));
    }

    [Fact]
    public void Create_EqualInputs_IsStable()
    {
        var first = CreateGroupName();
        var second = CreateGroupName();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_DifferentOwner_Differs()
    {
        var baseline = CreateGroupName();

        var actual = CreateGroupName(ownerId: "tenant-2");

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void Create_DifferentProvider_Differs()
    {
        var baseline = CreateGroupName();

        var actual = CreateGroupName(
            algorithm: CreateAlgorithm(provider: RemoteKeyProviderNames.AzureKeyVault)
        );

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void Create_DifferentAlgorithm_Differs()
    {
        var baseline = CreateGroupName();

        var actual = CreateGroupName(algorithm: CreateAlgorithm(algorithm: "ES256K"));

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void Create_DifferentKeySpec_Differs()
    {
        var baseline = CreateGroupName();

        var actual = CreateGroupName(algorithm: CreateAlgorithm(keySpec: "ECC_SECG_P256K1"));

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void Create_DifferentKeySize_Differs()
    {
        var baseline = CreateGroupName();

        var actual = CreateGroupName(algorithm: CreateAlgorithm(keySizeBits: 384));

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void Create_DifferentCurve_Differs()
    {
        var baseline = CreateGroupName();

        var actual = CreateGroupName(algorithm: CreateAlgorithm(curve: "secp256k1"));

        Assert.NotEqual(baseline, actual);
    }

    [Fact]
    public void Create_NullCurve_Differs()
    {
        var baseline = CreateGroupName();

        var actual = CreateGroupName(algorithm: CreateAlgorithm(curve: null));

        Assert.NotEqual(baseline, actual);
        Assert.Equal(37, actual.Length);
    }

    #endregion
}
