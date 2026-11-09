// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Keys;

// See ADR-1471.
public class RemoteRsaSecretKeyTests : BaseTests
{
    private const string KeyId = "key-1";

    private static byte[] SigningInput { get; } = "signing input"u8.ToArray();

    private static RemoteSigningAlgorithm Row { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "PS256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            SigningAlgorithm = "RSASSA_PSS_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
        };

    private static RemoteDecryptionAlgorithm DecryptionRow { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RSA-OAEP-256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

    private Mock<IRemoteKeyProvider> Provider { get; }

    private RSA LocalKey { get; } = RSA.Create(2048);

    public RemoteRsaSecretKeyTests()
    {
        Provider = CreateStrictMock<IRemoteKeyProvider>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            LocalKey.Dispose();

        base.Dispose(disposing);
    }

    private RemoteRsaSecretKey CreateKey() => CreateKey(Row);

    private RemoteRsaSecretKey CreateKey(RemoteKeyAlgorithm row) =>
        new(
            new KeyMetadata { KeyId = "kid" },
            2048,
            LocalKey.ExportSubjectPublicKeyInfo(),
            new RemoteKeyBinding
            {
                Provider = Provider.Object,
                Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, KeyId),
                Algorithm = row,
            }
        );

    #region ExportRSA Tests

    [Fact]
    public void ExportRSA_Always_ExportsOnlyPublicParameters()
    {
        var key = CreateKey();

        using var rsa = key.ExportRSA();

        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        Assert.Equal(LocalKey.ExportParameters(false).Modulus, parameters.Modulus);
        Assert.Null(parameters.D);
        Assert.Throws<CryptographicException>(() =>
            rsa.ExportParameters(includePrivateParameters: true)
        );
    }

    #endregion

    #region SignAsync Tests

    [Fact]
    public async Task SignAsync_WhenAlgorithmIsTheRows_SignsThroughTheProvider()
    {
        var digest = SHA256.HashData(SigningInput);
        Provider
            .Setup(x =>
                x.SignAsync(
                    It.Is<RemoteSignatureRequest>(r =>
                        r.KeyId == KeyId
                        && ReferenceEquals(r.Algorithm, Row)
                        && r.Data.ToArray().SequenceEqual(digest)
                    ),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (RemoteSignatureRequest _, Memory<byte> destination, CancellationToken _) =>
                {
                    var bytes = LocalKey.SignHash(
                        digest,
                        HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pss
                    );
                    bytes.CopyTo(destination);
                    return ValueTask.FromResult(bytes.Length);
                }
            )
            .Verifiable();
        var key = CreateKey();
        var signature = new byte[256];

        var written = await key.SignAsync(
            Row.Algorithm,
            SigningInput,
            signature,
            CancellationToken.None
        );

        Assert.Equal(256, written);
        Assert.True(
            LocalKey.VerifyData(
                SigningInput,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss
            )
        );
    }

    [Fact]
    public async Task SignAsync_WhenAlgorithmDiffersFromTheRows_ThrowsWithoutCallingTheProvider()
    {
        var key = CreateKey();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await key.SignAsync("RS256", SigningInput, new byte[256], CancellationToken.None)
        );
    }

    #endregion

    #region DecryptAsync Tests

    [Fact]
    public async Task DecryptAsync_WhenAlgorithmIsTheRows_DecryptsThroughTheProvider()
    {
        byte[] ciphertext = [1, 2, 3, 4];
        byte[] providerPlaintext = [9, 8, 7];
        Provider
            .Setup(x =>
                x.DecryptAsync(
                    It.Is<RemoteDecryptionRequest>(r =>
                        r.KeyId == KeyId
                        && ReferenceEquals(r.Algorithm, DecryptionRow)
                        && r.Ciphertext.ToArray().SequenceEqual(ciphertext)
                    ),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (RemoteDecryptionRequest _, Memory<byte> destination, CancellationToken _) =>
                {
                    providerPlaintext.CopyTo(destination);
                    return ValueTask.FromResult(providerPlaintext.Length);
                }
            )
            .Verifiable();
        var key = CreateKey(DecryptionRow);
        var plaintext = new byte[8];

        var written = await key.DecryptAsync(
            DecryptionRow.Algorithm,
            ciphertext,
            plaintext,
            CancellationToken.None
        );

        Assert.Equal(3, written);
        Assert.Equal([9, 8, 7, 0, 0, 0, 0, 0], plaintext);
    }

    [Fact]
    public async Task DecryptAsync_WhenAlgorithmDiffersFromTheRows_ThrowsWithoutCallingTheProvider()
    {
        var key = CreateKey(DecryptionRow);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await key.DecryptAsync("RSA-OAEP", new byte[4], new byte[8], CancellationToken.None)
        );
    }

    #endregion

    #region DeriveSharedSecretAsync Tests

    [Fact]
    public async Task DeriveSharedSecretAsync_Always_ThrowsWithoutCallingTheProvider()
    {
        var key = CreateKey(DecryptionRow);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await key.DeriveSharedSecretAsync(
                "ECDH-ES",
                new byte[4],
                new byte[32],
                CancellationToken.None
            )
        );
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_Always_AppendsTheProviderName()
    {
        var key = CreateKey();

        var text = key.ToString();

        Assert.Equal("rsa { KeyId = 'kid', Size = 2048, Provider = 'aws-kms' }", text);
    }

    #endregion
}
