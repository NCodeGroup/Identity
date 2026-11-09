// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Jose.Tests;

// See ADR-1479.
public class DefaultRemoteKeyProverTests : BaseTests
{
    private const string RemoteKeyId = "remote-key-1";
    private const string RemoteKid = "remote-kid";

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

    private static readonly RemoteSigningAlgorithm UnregisteredRow = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "XX999",
        SecretType = "ecc",
        KeySizeBits = 256,
        Curve = "P-256",
        KeySpec = "ECC_NIST_P256",
        SigningAlgorithm = "ECDSA_SHA_256",
        DigestAlgorithm = HashAlgorithmName.SHA256,
        IsDerEncodedSignature = true,
    };

    private static readonly RemoteDecryptionAlgorithm RsaOaep256Row = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "RSA-OAEP-256",
        SecretType = "rsa",
        KeySizeBits = 2048,
        KeySpec = "RSA_2048",
        EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
    };

    private ServiceProvider ServiceProvider { get; }

    private IRemoteKeyProver Prover { get; }

    private Mock<IRemoteKeyProvider> Provider { get; }

    private ECDsa HeldEcdsa { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private ECDsa OtherEcdsa { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private RSA HeldRsa { get; } = RSA.Create(2048);

    private RSA OtherRsa { get; } = RSA.Create(2048);

    public DefaultRemoteKeyProverTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Prover = ServiceProvider.GetRequiredService<IRemoteKeyProver>();

        Provider = CreateStrictMock<IRemoteKeyProvider>();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            HeldEcdsa.Dispose();
            OtherEcdsa.Dispose();
            HeldRsa.Dispose();
            OtherRsa.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Helpers

    private RemoteKeyBinding CreateBinding(RemoteKeyAlgorithm row) =>
        new()
        {
            Provider = Provider.Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, RemoteKeyId),
            Algorithm = row,
        };

    private static KeyMetadata CreateMetadata() => new() { KeyId = RemoteKid };

    private SecretKey CreateRemoteEccKey(RemoteSigningAlgorithm row) =>
        SecretKeyFactory.CreateEccRemote(
            CreateMetadata(),
            HeldEcdsa.ExportSubjectPublicKeyInfo(),
            CreateBinding(row)
        );

    private SecretKey CreateRemoteRsaKey() =>
        SecretKeyFactory.CreateRsaRemote(
            CreateMetadata(),
            HeldRsa.ExportSubjectPublicKeyInfo(),
            CreateBinding(RsaOaep256Row)
        );

    private void SetupSign(ECDsa signer) =>
        Provider
            .Setup(x =>
                x.SignAsync(
                    It.Is<RemoteSignatureRequest>(r => r.KeyId == RemoteKeyId),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (RemoteSignatureRequest request, Memory<byte> destination, CancellationToken _) =>
                {
                    var bytes = signer.SignHash(
                        request.Data.Span,
                        DSASignatureFormat.Rfc3279DerSequence
                    );
                    bytes.CopyTo(destination);
                    return ValueTask.FromResult(bytes.Length);
                }
            )
            .Verifiable(Times.Once());

    private void SetupDecrypt(RSA decrypter) =>
        Provider
            .Setup(x =>
                x.DecryptAsync(
                    It.Is<RemoteDecryptionRequest>(r => r.KeyId == RemoteKeyId),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (RemoteDecryptionRequest request, Memory<byte> plaintext, CancellationToken _) =>
                {
                    var bytes = decrypter.Decrypt(
                        request.Ciphertext.ToArray(),
                        RSAEncryptionPadding.OaepSHA256
                    );
                    bytes.CopyTo(plaintext);
                    return ValueTask.FromResult(bytes.Length);
                }
            )
            .Verifiable(Times.Once());

    #endregion

    #region ProveAsync Tests

    [Fact]
    public async Task ProveAsync_WhenTheServiceSignsWithTheKey_Completes()
    {
        SetupSign(HeldEcdsa);
        var remoteKey = CreateRemoteEccKey(Es256Row);

        await Prover.ProveAsync(remoteKey, CancellationToken.None);
    }

    [Fact]
    public async Task ProveAsync_WhenTheServiceSignsWithAnotherKey_ThrowsRefused()
    {
        SetupSign(OtherEcdsa);
        var remoteKey = CreateRemoteEccKey(Es256Row);

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await Prover.ProveAsync(remoteKey, CancellationToken.None)
        );

        Assert.Contains("does not sign with the private half", exception.Message);
    }

    [Fact]
    public async Task ProveAsync_WhenTheServiceDecryptsWithTheKey_Completes()
    {
        SetupDecrypt(HeldRsa);
        var remoteKey = CreateRemoteRsaKey();

        await Prover.ProveAsync(remoteKey, CancellationToken.None);
    }

    [Fact]
    public async Task ProveAsync_WhenTheServiceDecryptsWithAnotherKey_ThrowsRefused()
    {
        SetupDecrypt(OtherRsa);
        var remoteKey = CreateRemoteRsaKey();

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await Prover.ProveAsync(remoteKey, CancellationToken.None)
        );

        Assert.Contains("does not decrypt with the private half", exception.Message);
    }

    [Fact]
    public async Task ProveAsync_WhenTheServiceFails_PropagatesItsException()
    {
        Provider
            .Setup(x =>
                x.SignAsync(
                    It.IsAny<RemoteSignatureRequest>(),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(() => ValueTask.FromException<int>(new IOException("unreachable")))
            .Verifiable(Times.Once());
        var remoteKey = CreateRemoteEccKey(Es256Row);

        await Assert.ThrowsAsync<IOException>(async () =>
            await Prover.ProveAsync(remoteKey, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ProveAsync_WhenTheKeyIsNotRemote_Throws()
    {
        var localKey = SecretKeyFactory.CreateEccSpki(
            CreateMetadata(),
            HeldEcdsa.ExportSubjectPublicKeyInfo()
        );

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Prover.ProveAsync(localKey, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ProveAsync_WhenTheAlgorithmIsNotRegistered_ThrowsRefused()
    {
        var remoteKey = CreateRemoteEccKey(UnregisteredRow);

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await Prover.ProveAsync(remoteKey, CancellationToken.None)
        );

        Assert.Contains("'XX999'", exception.Message);
    }

    #endregion
}
