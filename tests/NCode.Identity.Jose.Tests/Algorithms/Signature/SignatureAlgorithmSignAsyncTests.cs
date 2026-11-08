// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms.Signature;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Jose.Tests.Algorithms.Signature;

// See ADR-1472.
public class SignatureAlgorithmSignAsyncTests : BaseTests
{
    private const string KeyId = "key-1";

    private static readonly byte[] SigningInput = "signing input"u8.ToArray();

    private static readonly RemoteSigningAlgorithm Ps256Row = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "PS256",
        SecretType = "rsa",
        KeySizeBits = 2048,
        KeySpec = "RSA_2048",
        SigningAlgorithm = "RSASSA_PSS_SHA_256",
        DigestAlgorithm = HashAlgorithmName.SHA256,
    };

    private static readonly RemoteSigningAlgorithm Rs256Row = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "RS256",
        SecretType = "rsa",
        KeySizeBits = 2048,
        KeySpec = "RSA_2048",
        SigningAlgorithm = "RSASSA_PKCS1_V1_5_SHA_256",
        DigestAlgorithm = HashAlgorithmName.SHA256,
    };

    private static readonly RemoteSigningAlgorithm Es256Row = new()
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

    private RSA HeldRsa { get; } = RSA.Create(2048);

    private RSA OtherRsa { get; } = RSA.Create(2048);

    private ECDsa HeldEcdsa { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private ECDsa OtherEcdsa { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public SignatureAlgorithmSignAsyncTests()
    {
        Provider = CreateStrictMock<IRemoteKeyProvider>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            HeldRsa.Dispose();
            OtherRsa.Dispose();
            HeldEcdsa.Dispose();
            OtherEcdsa.Dispose();
        }

        base.Dispose(disposing);
    }

    private RemoteKeyBinding CreateBinding(RemoteSigningAlgorithm row) =>
        new()
        {
            Provider = Provider.Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, KeyId),
            Algorithm = row,
        };

    private RemoteRsaSecretKey CreateRemoteRsaKey(RemoteSigningAlgorithm row) =>
        new(
            new KeyMetadata { KeyId = "kid" },
            2048,
            HeldRsa.ExportSubjectPublicKeyInfo(),
            CreateBinding(row)
        );

    private RemoteEccSecretKey CreateRemoteEccKey() =>
        new(
            new KeyMetadata { KeyId = "kid" },
            EccCurveSpecification.P256.CurveName,
            256,
            HeldEcdsa.ExportSubjectPublicKeyInfo(),
            CreateBinding(Es256Row)
        );

    private static RsaSignatureAlgorithm CreatePs256Algorithm() =>
        new("PS256", HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

    private static RsaSignatureAlgorithm CreateRs256Algorithm() =>
        new("RS256", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    private static EccSignatureAlgorithm CreateEs256Algorithm() =>
        EccSignatureAlgorithmFactory.Create(
            "ES256",
            ECCurve.NamedCurves.nistP256,
            HashAlgorithmName.SHA256
        );

    private void SetupProviderReturns(byte[] providerSignature) =>
        Provider
            .Setup(x =>
                x.SignAsync(
                    It.Is<RemoteSignatureRequest>(r => r.KeyId == KeyId),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(providerSignature)
            .Verifiable();

    #region SignAsync Tests

    [Fact]
    public async Task SignAsync_WhenRemoteRsaKeyAndPs256_ReturnsSignatureThatVerifies()
    {
        var digest = SHA256.HashData(SigningInput);
        SetupProviderReturns(
            HeldRsa.SignHash(digest, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
        );
        var algorithm = CreatePs256Algorithm();
        var secretKey = CreateRemoteRsaKey(Ps256Row);
        var signature = new byte[256];

        var written = await algorithm.SignAsync(
            secretKey,
            SigningInput,
            signature,
            CancellationToken.None
        );

        Assert.Equal(256, written);
        Assert.True(
            HeldRsa.VerifyData(
                SigningInput,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss
            )
        );
    }

    [Fact]
    public async Task SignAsync_WhenRemoteRsaKeyAndRs256_ReturnsSignatureThatVerifies()
    {
        var digest = SHA256.HashData(SigningInput);
        SetupProviderReturns(
            HeldRsa.SignHash(digest, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
        );
        var algorithm = CreateRs256Algorithm();
        var secretKey = CreateRemoteRsaKey(Rs256Row);
        var signature = new byte[256];

        var written = await algorithm.SignAsync(
            secretKey,
            SigningInput,
            signature,
            CancellationToken.None
        );

        Assert.Equal(256, written);
        Assert.True(
            HeldRsa.VerifyData(
                SigningInput,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            )
        );
    }

    [Fact]
    public async Task SignAsync_WhenRemoteEccKeyAndEs256_ReturnsSignatureThatVerifies()
    {
        var digest = SHA256.HashData(SigningInput);
        SetupProviderReturns(HeldEcdsa.SignHash(digest, DSASignatureFormat.Rfc3279DerSequence));
        var algorithm = CreateEs256Algorithm();
        var secretKey = CreateRemoteEccKey();
        var signature = new byte[64];

        var written = await algorithm.SignAsync(
            secretKey,
            SigningInput,
            signature,
            CancellationToken.None
        );

        Assert.Equal(64, written);
        Assert.True(
            HeldEcdsa.VerifyData(
                SigningInput,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation
            )
        );
    }

    [Fact]
    public async Task SignAsync_WhenRemoteRsaProviderReturnsAnotherKeysSignature_ThrowsJoseException()
    {
        var digest = SHA256.HashData(SigningInput);
        SetupProviderReturns(
            OtherRsa.SignHash(digest, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
        );
        var algorithm = CreatePs256Algorithm();
        var secretKey = CreateRemoteRsaKey(Ps256Row);

        var exception = await Assert.ThrowsAsync<JoseException>(async () =>
            await algorithm.SignAsync(
                secretKey,
                SigningInput,
                new byte[256],
                CancellationToken.None
            )
        );

        Assert.Equal(
            $"The remote key '{KeyId}' returned a signature that its public key does not verify.",
            exception.Message
        );
    }

    [Fact]
    public async Task SignAsync_WhenRemoteEccProviderReturnsAnotherKeysSignature_ThrowsJoseException()
    {
        var digest = SHA256.HashData(SigningInput);
        SetupProviderReturns(OtherEcdsa.SignHash(digest, DSASignatureFormat.Rfc3279DerSequence));
        var algorithm = CreateEs256Algorithm();
        var secretKey = CreateRemoteEccKey();

        await Assert.ThrowsAsync<JoseException>(async () =>
            await algorithm.SignAsync(secretKey, SigningInput, new byte[64], CancellationToken.None)
        );
    }

    [Fact]
    public async Task SignAsync_WhenRemoteProviderReturnsAShortSignature_ThrowsJoseException()
    {
        var digest = SHA256.HashData(SigningInput);
        var fullSignature = HeldRsa.SignHash(
            digest,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss
        );
        SetupProviderReturns(fullSignature[..128]);
        var algorithm = CreatePs256Algorithm();
        var secretKey = CreateRemoteRsaKey(Ps256Row);

        await Assert.ThrowsAsync<JoseException>(async () =>
            await algorithm.SignAsync(
                secretKey,
                SigningInput,
                new byte[256],
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task SignAsync_WhenRemoteKeyIsIncompatible_ThrowsBeforeCallingTheProvider()
    {
        var algorithm = CreateRs256Algorithm();
        var secretKey = CreateRemoteEccKey();

        await Assert.ThrowsAsync<JoseInvalidAlgorithmException>(async () =>
            await algorithm.SignAsync(
                secretKey,
                SigningInput,
                new byte[256],
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task SignAsync_WhenLocalKey_CompletesSynchronouslyWithAVerifiableSignature()
    {
        using var rsa = RSA.Create(2048);
        var secretKey = SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "local" }, rsa);
        var algorithm = CreatePs256Algorithm();
        var signature = new byte[256];

        var task = algorithm.SignAsync(secretKey, SigningInput, signature, CancellationToken.None);

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(256, await task);
        Assert.True(algorithm.Verify(secretKey, SigningInput, signature));
    }

    [Fact]
    public async Task SignAsync_WhenLocalKeyAndSignatureBufferIsTooSmall_ThrowsJoseException()
    {
        using var rsa = RSA.Create(2048);
        var secretKey = SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "local" }, rsa);
        var algorithm = CreatePs256Algorithm();

        await Assert.ThrowsAsync<JoseException>(async () =>
            await algorithm.SignAsync(secretKey, SigningInput, new byte[16], CancellationToken.None)
        );
    }

    #endregion
}
