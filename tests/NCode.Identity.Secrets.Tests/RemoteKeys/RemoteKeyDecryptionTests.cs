// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.RemoteKeys;

// See ADR-1477.
public class RemoteKeyDecryptionTests : BaseTests
{
    private const string KeyId = "key-1";

    private static readonly byte[] Ciphertext = [1, 2, 3, 4];

    private static readonly byte[] PeerPublicKey = [5, 6, 7, 8];

    private static readonly RemoteDecryptionAlgorithm DecryptionRow = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "RSA-OAEP-256",
        SecretType = "rsa",
        KeySizeBits = 2048,
        KeySpec = "RSA_2048",
        EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
    };

    private static readonly RemoteKeyAgreementAlgorithm AgreementRow = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "ECDH-ES",
        SecretType = "ecc",
        KeySizeBits = 256,
        Curve = "P-256",
        KeySpec = "ECC_NIST_P256",
        KeyAgreementAlgorithm = "ECDH",
    };

    private static readonly RemoteSigningAlgorithm SigningRow = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "PS256",
        SecretType = "rsa",
        KeySizeBits = 2048,
        KeySpec = "RSA_2048",
        SigningAlgorithm = "RSASSA_PSS_SHA_256",
    };

    private Mock<IRemoteKeyProvider> Provider { get; }

    public RemoteKeyDecryptionTests()
    {
        Provider = CreateStrictMock<IRemoteKeyProvider>();
    }

    private RemoteKeyBinding CreateBinding(RemoteKeyAlgorithm algorithm) =>
        new()
        {
            Provider = Provider.Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, KeyId),
            Algorithm = algorithm,
        };

    #region DecryptAsync Tests

    [Fact]
    public async Task DecryptAsync_WhenRowDecrypts_CallsTheProviderWithTheKeyAndRow()
    {
        byte[] providerPlaintext = [9, 8, 7];
        Provider
            .Setup(x =>
                x.DecryptAsync(
                    It.Is<RemoteDecryptionRequest>(r =>
                        r.KeyId == KeyId
                        && ReferenceEquals(r.Algorithm, DecryptionRow)
                        && r.Ciphertext.ToArray().SequenceEqual(Ciphertext)
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
        var plaintext = new byte[8];

        var written = await RemoteKeyDecryption.DecryptAsync(
            CreateBinding(DecryptionRow),
            Ciphertext,
            plaintext,
            CancellationToken.None
        );

        Assert.Equal(3, written);
        Assert.Equal([9, 8, 7, 0, 0, 0, 0, 0], plaintext);
    }

    [Fact]
    public async Task DecryptAsync_WhenProviderReportsMoreThanTheBuffer_ThrowsCryptographicException()
    {
        Provider
            .Setup(x =>
                x.DecryptAsync(
                    It.IsAny<RemoteDecryptionRequest>(),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(9)
            .Verifiable();

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await RemoteKeyDecryption.DecryptAsync(
                CreateBinding(DecryptionRow),
                Ciphertext,
                new byte[8],
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task DecryptAsync_WhenRowIsASigningRow_ThrowsWithoutCallingTheProvider()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await RemoteKeyDecryption.DecryptAsync(
                CreateBinding(SigningRow),
                Ciphertext,
                new byte[8],
                CancellationToken.None
            )
        );
    }

    #endregion

    #region DeriveSharedSecretAsync Tests

    [Fact]
    public async Task DeriveSharedSecretAsync_WhenRowAgrees_CallsTheProviderWithTheKeyAndRow()
    {
        byte[] providerSecret = [4, 3, 2, 1];
        Provider
            .Setup(x =>
                x.DeriveSharedSecretAsync(
                    It.Is<RemoteKeyAgreementRequest>(r =>
                        r.KeyId == KeyId
                        && ReferenceEquals(r.Algorithm, AgreementRow)
                        && r.PeerSubjectPublicKeyInfo.ToArray().SequenceEqual(PeerPublicKey)
                    ),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (RemoteKeyAgreementRequest _, Memory<byte> destination, CancellationToken _) =>
                {
                    providerSecret.CopyTo(destination);
                    return ValueTask.FromResult(providerSecret.Length);
                }
            )
            .Verifiable();
        var sharedSecret = new byte[6];

        var written = await RemoteKeyDecryption.DeriveSharedSecretAsync(
            CreateBinding(AgreementRow),
            PeerPublicKey,
            sharedSecret,
            CancellationToken.None
        );

        Assert.Equal(4, written);
        Assert.Equal([4, 3, 2, 1, 0, 0], sharedSecret);
    }

    [Fact]
    public async Task DeriveSharedSecretAsync_WhenProviderReportsMoreThanTheBuffer_ThrowsCryptographicException()
    {
        Provider
            .Setup(x =>
                x.DeriveSharedSecretAsync(
                    It.IsAny<RemoteKeyAgreementRequest>(),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(7)
            .Verifiable();

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await RemoteKeyDecryption.DeriveSharedSecretAsync(
                CreateBinding(AgreementRow),
                PeerPublicKey,
                new byte[6],
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task DeriveSharedSecretAsync_WhenRowIsASigningRow_ThrowsWithoutCallingTheProvider()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await RemoteKeyDecryption.DeriveSharedSecretAsync(
                CreateBinding(SigningRow),
                PeerPublicKey,
                new byte[6],
                CancellationToken.None
            )
        );
    }

    #endregion
}
