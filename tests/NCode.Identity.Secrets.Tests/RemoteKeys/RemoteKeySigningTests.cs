// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.RemoteKeys;

// See ADR-1471.
public class RemoteKeySigningTests : BaseTests
{
    private const string KeyId = "key-1";

    private static readonly byte[] SigningInput = "signing input"u8.ToArray();

    private Mock<IRemoteKeyProvider> Provider { get; }

    public RemoteKeySigningTests()
    {
        Provider = CreateStrictMock<IRemoteKeyProvider>();
    }

    private static RemoteSigningAlgorithm CreateRsaRow(HashAlgorithmName? digestAlgorithm) =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "PS256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            SigningAlgorithm = "RSASSA_PSS_SHA_256",
            DigestAlgorithm = digestAlgorithm,
        };

    private static RemoteSigningAlgorithm CreateEcdsaRow() =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "ES256",
            SecretType = "ecc",
            KeySizeBits = 256,
            Curve = "P-256",
            KeySpec = "ECC_NIST_P256",
            SigningAlgorithm = "ECDSA_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
            IsDerEncodedSignature = true,
        };

    private RemoteKeyBinding CreateBinding(RemoteSigningAlgorithm algorithm) =>
        new()
        {
            Provider = Provider.Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, KeyId),
            Algorithm = algorithm,
        };

    private static bool HasRequest(
        RemoteSignatureRequest request,
        RemoteSigningAlgorithm algorithm,
        byte[] data
    ) =>
        request.KeyId == KeyId
        && ReferenceEquals(request.Algorithm, algorithm)
        && request.Data.ToArray().SequenceEqual(data);

    #region SignAsync Tests

    [Fact]
    public async Task SignAsync_WhenRowHasDigest_SendsTheDigestAndCopiesTheSignature()
    {
        var algorithm = CreateRsaRow(HashAlgorithmName.SHA256);
        var digest = SHA256.HashData(SigningInput);
        byte[] providerSignature = [1, 2, 3, 4];
        Provider
            .Setup(x =>
                x.SignAsync(
                    It.Is<RemoteSignatureRequest>(r => HasRequest(r, algorithm, digest)),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(providerSignature)
            .Verifiable();
        var signature = new byte[8];

        var written = await RemoteKeySigning.SignAsync(
            CreateBinding(algorithm),
            SigningInput,
            signature,
            CancellationToken.None
        );

        Assert.Equal(4, written);
        Assert.Equal([1, 2, 3, 4, 0, 0, 0, 0], signature);
    }

    [Fact]
    public async Task SignAsync_WhenRowHasNoDigest_SendsTheSigningInput()
    {
        var algorithm = CreateRsaRow(digestAlgorithm: null);
        byte[] providerSignature = [9, 8, 7];
        Provider
            .Setup(x =>
                x.SignAsync(
                    It.Is<RemoteSignatureRequest>(r => HasRequest(r, algorithm, SigningInput)),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(providerSignature)
            .Verifiable();
        var signature = new byte[3];

        var written = await RemoteKeySigning.SignAsync(
            CreateBinding(algorithm),
            SigningInput,
            signature,
            CancellationToken.None
        );

        Assert.Equal(3, written);
        Assert.Equal(providerSignature, signature);
    }

    [Fact]
    public async Task SignAsync_WhenRowIsDerEncoded_ConvertsTheSignatureToIeeeP1363()
    {
        var algorithm = CreateEcdsaRow();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var digest = SHA256.HashData(SigningInput);
        var der = ecdsa.SignHash(digest, DSASignatureFormat.Rfc3279DerSequence);
        Provider
            .Setup(x =>
                x.SignAsync(
                    It.Is<RemoteSignatureRequest>(r => HasRequest(r, algorithm, digest)),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(der)
            .Verifiable();
        var signature = new byte[64];

        var written = await RemoteKeySigning.SignAsync(
            CreateBinding(algorithm),
            SigningInput,
            signature,
            CancellationToken.None
        );

        Assert.Equal(64, written);
        Assert.True(
            ecdsa.VerifyData(
                SigningInput,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation
            )
        );
    }

    [Fact]
    public async Task SignAsync_WhenRowIsNotDerEncoded_DoesNotConvertTheSignature()
    {
        var algorithm = CreateRsaRow(HashAlgorithmName.SHA256);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var der = ecdsa.SignHash(
            SHA256.HashData(SigningInput),
            DSASignatureFormat.Rfc3279DerSequence
        );
        Provider
            .Setup(x => x.SignAsync(It.IsAny<RemoteSignatureRequest>(), CancellationToken.None))
            .ReturnsAsync(der)
            .Verifiable();
        var signature = new byte[der.Length];

        var written = await RemoteKeySigning.SignAsync(
            CreateBinding(algorithm),
            SigningInput,
            signature,
            CancellationToken.None
        );

        Assert.Equal(der.Length, written);
        Assert.Equal(der, signature);
    }

    [Fact]
    public async Task SignAsync_WhenCopiedSignatureExceedsTheDestination_Throws()
    {
        var algorithm = CreateRsaRow(HashAlgorithmName.SHA256);
        Provider
            .Setup(x => x.SignAsync(It.IsAny<RemoteSignatureRequest>(), CancellationToken.None))
            .ReturnsAsync([1, 2, 3, 4])
            .Verifiable();
        var binding = CreateBinding(algorithm);

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await RemoteKeySigning.SignAsync(
                binding,
                SigningInput,
                new byte[3],
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task SignAsync_WhenConvertedSignatureExceedsTheDestination_Throws()
    {
        var algorithm = CreateEcdsaRow();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var der = ecdsa.SignHash(
            SHA256.HashData(SigningInput),
            DSASignatureFormat.Rfc3279DerSequence
        );
        Provider
            .Setup(x => x.SignAsync(It.IsAny<RemoteSignatureRequest>(), CancellationToken.None))
            .ReturnsAsync(der)
            .Verifiable();
        var binding = CreateBinding(algorithm);

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await RemoteKeySigning.SignAsync(
                binding,
                SigningInput,
                new byte[63],
                CancellationToken.None
            )
        );
    }

    #endregion
}
