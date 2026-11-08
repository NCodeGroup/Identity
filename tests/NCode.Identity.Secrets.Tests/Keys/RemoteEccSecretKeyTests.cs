// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Keys;

// See ADR-1471.
public class RemoteEccSecretKeyTests : BaseTests
{
    private const string KeyId = "key-1";

    private static readonly byte[] SigningInput = "signing input"u8.ToArray();

    private static readonly RemoteSigningAlgorithm Row = new()
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

    private Mock<IRemoteKeyProvider> Provider { get; }

    private ECDsa LocalKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public RemoteEccSecretKeyTests()
    {
        Provider = CreateStrictMock<IRemoteKeyProvider>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            LocalKey.Dispose();

        base.Dispose(disposing);
    }

    private RemoteEccSecretKey CreateKey() =>
        new(
            new KeyMetadata { KeyId = "kid" },
            EccCurveSpecification.P256.CurveName,
            256,
            LocalKey.ExportSubjectPublicKeyInfo(),
            new RemoteKeyBinding
            {
                Provider = Provider.Object,
                Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, KeyId),
                Algorithm = Row,
            }
        );

    #region Export Tests

    [Fact]
    public void ExportECDsa_Always_ExportsOnlyPublicParameters()
    {
        var key = CreateKey();

        using var ecdsa = key.ExportECDsa();

        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        Assert.Equal(LocalKey.ExportParameters(false).Q.X, parameters.Q.X);
        Assert.Null(parameters.D);
        Assert.Throws<CryptographicException>(() =>
            ecdsa.ExportParameters(includePrivateParameters: true)
        );
    }

    [Fact]
    public void ExportECDiffieHellman_Always_ExportsOnlyPublicParameters()
    {
        var key = CreateKey();

        using var ecDiffieHellman = key.ExportECDiffieHellman();

        var parameters = ecDiffieHellman.ExportParameters(includePrivateParameters: false);
        Assert.Equal(LocalKey.ExportParameters(false).Q.X, parameters.Q.X);
        Assert.Null(parameters.D);
        Assert.Throws<CryptographicException>(() =>
            ecDiffieHellman.ExportParameters(includePrivateParameters: true)
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
                    CancellationToken.None
                )
            )
            .ReturnsAsync(LocalKey.SignHash(digest, DSASignatureFormat.Rfc3279DerSequence))
            .Verifiable();
        var key = CreateKey();
        var signature = new byte[64];

        var written = await key.SignAsync(
            Row.Algorithm,
            SigningInput,
            signature,
            CancellationToken.None
        );

        Assert.Equal(64, written);
        Assert.True(
            LocalKey.VerifyData(
                SigningInput,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation
            )
        );
    }

    [Fact]
    public async Task SignAsync_WhenAlgorithmDiffersFromTheRows_ThrowsWithoutCallingTheProvider()
    {
        var key = CreateKey();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await key.SignAsync("ES384", SigningInput, new byte[96], CancellationToken.None)
        );
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_Always_AppendsTheProviderName()
    {
        var key = CreateKey();

        var text = key.ToString();

        Assert.Equal("ecc { KeyId = 'kid', Size = 256, Provider = 'aws-kms' }", text);
    }

    #endregion
}
