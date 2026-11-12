// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Jose.Tests;

// See ADR-1471.
public class RemoteKeyJweTests : BaseTests
{
    private const string Payload = """{"foo":"bar","n":123}""";
    private const string RemoteKeyId = "remote-key-1";
    private const string RemoteKid = "remote-kid";

    private static RemoteDecryptionAlgorithm RsaOaep256Row { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RSA-OAEP-256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

    private static RemoteKeyAgreementAlgorithm EcdhEsA256KwRow { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "ECDH-ES+A256KW",
            SecretType = "ecc",
            KeySizeBits = 256,
            Curve = "P-256",
            KeySpec = "ECC_NIST_P256",
            KeyAgreementAlgorithm = "ECDH",
        };

    private ServiceProvider ServiceProvider { get; }

    private IJoseSerializer JoseSerializer { get; }

    private IAlgorithmCollection AlgorithmCollection { get; }

    private Mock<IRemoteKeyProvider> Provider { get; }

    private RSA HeldRsa { get; } = RSA.Create(2048);

    private ECDiffieHellman HeldEcdh { get; } =
        ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    public RemoteKeyJweTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        JoseSerializer = ServiceProvider.GetRequiredService<IJoseSerializer>();
        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;

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
            HeldRsa.Dispose();
            HeldEcdh.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Helpers

    private KeyManagementAlgorithm GetKeyManagementAlgorithm(string code)
    {
        Assert.True(AlgorithmCollection.TryGetKeyManagementAlgorithm(code, out var algorithm));
        return algorithm;
    }

    private AuthenticatedEncryptionAlgorithm GetAuthenticatedEncryptionAlgorithm(string code)
    {
        Assert.True(
            AlgorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(code, out var algorithm)
        );
        return algorithm;
    }

    private RemoteKeyBinding CreateBinding(RemoteKeyAlgorithm row) =>
        new()
        {
            Provider = Provider.Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, RemoteKeyId),
            Algorithm = row,
        };

    private static KeyMetadata CreateMetadata() =>
        new() { KeyId = RemoteKid, Use = SecretKeyUses.Encryption };

    private SecretKey CreateRemoteRsaKey() =>
        SecretKeyFactory.CreateRsaRemote(
            CreateMetadata(),
            HeldRsa.ExportSubjectPublicKeyInfo(),
            CreateBinding(RsaOaep256Row)
        );

    private SecretKey CreateRemoteEccKey() =>
        SecretKeyFactory.CreateEccRemote(
            CreateMetadata(),
            HeldEcdh.ExportSubjectPublicKeyInfo(),
            CreateBinding(EcdhEsA256KwRow)
        );

    private JoseEncryptionCredentials CreateCredentials(SecretKey secretKey, string code) =>
        new(
            secretKey,
            GetKeyManagementAlgorithm(code),
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm),
            null
        );

    private ValueTask<string> EncodeCompactAsync(
        SecretKey secretKey,
        string code,
        CancellationToken cancellationToken
    ) =>
        JoseSerializer
            .CreateEncoder(new JoseEncryptionOptions(CreateCredentials(secretKey, code)))
            .EncodeAsync(Payload, cancellationToken);

    private ValueTask<string> EncodeFlattenedJsonAsync(
        SecretKey remoteKey,
        CancellationToken cancellationToken
    )
    {
        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.Flattened,
            Recipients =
            [
                new JoseRecipientOptions(
                    new JoseKeyManagementCredentials(
                        remoteKey,
                        GetKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.RsaOaep256)
                    )
                ),
            ],
        };
        return JoseSerializer.CreateEncoder(options).EncodeAsync(Payload, cancellationToken);
    }

    private void SetupDecrypt() =>
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
                    var bytes = HeldRsa.Decrypt(
                        request.Ciphertext.ToArray(),
                        RSAEncryptionPadding.OaepSHA256
                    );
                    bytes.CopyTo(plaintext);
                    return ValueTask.FromResult(bytes.Length);
                }
            )
            .Verifiable(Times.Once());

    private void SetupDeriveSharedSecret() =>
        Provider
            .Setup(x =>
                x.DeriveSharedSecretAsync(
                    It.Is<RemoteKeyAgreementRequest>(r => r.KeyId == RemoteKeyId),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (
                    RemoteKeyAgreementRequest request,
                    Memory<byte> sharedSecret,
                    CancellationToken cancellationToken
                ) =>
                {
                    using var peer = ECDiffieHellman.Create();
                    peer.ImportSubjectPublicKeyInfo(request.PeerSubjectPublicKeyInfo.Span, out _);
                    using var peerPublicKey = peer.PublicKey;
                    var secret = HeldEcdh.DeriveRawSecretAgreement(peerPublicKey);
                    secret.CopyTo(sharedSecret);
                    return ValueTask.FromResult(secret.Length);
                }
            )
            .Verifiable(Times.Once());

    #endregion

    #region DecodeAsync Tests

    [Fact]
    public async Task DecodeAsync_RsaOaep256ToARemoteKey_DecryptsThroughTheService()
    {
        SetupDecrypt();
        var remoteKey = CreateRemoteRsaKey();
        var token = await EncodeCompactAsync(
            remoteKey,
            AlgorithmCodes.KeyManagement.RsaOaep256,
            CancellationToken.None
        );

        var decoded = await JoseSerializer.DecodeAsync(token, remoteKey, CancellationToken.None);

        Assert.Equal(Payload, decoded);
    }

    [Fact]
    public async Task DecodeAsync_EcdhEsA256KwToARemoteKey_AgreesThroughTheService()
    {
        SetupDeriveSharedSecret();
        var remoteKey = CreateRemoteEccKey();
        var token = await EncodeCompactAsync(
            remoteKey,
            AlgorithmCodes.KeyManagement.EcdhEsAes256,
            CancellationToken.None
        );

        var decoded = await JoseSerializer.DecodeAsync(token, remoteKey, CancellationToken.None);

        Assert.Equal(Payload, decoded);
    }

    [Fact]
    public async Task DecodeAsync_ServiceRefusesTheCiphertext_FailsAtTheTag()
    {
        Provider
            .Setup(x =>
                x.DecryptAsync(
                    It.IsAny<RemoteDecryptionRequest>(),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(() => ValueTask.FromException<int>(new CryptographicException("refused")))
            .Verifiable();
        var remoteKey = CreateRemoteRsaKey();
        var token = await EncodeCompactAsync(
            remoteKey,
            AlgorithmCodes.KeyManagement.RsaOaep256,
            CancellationToken.None
        );

        await Assert.ThrowsAsync<JoseEncryptionException>(async () =>
            await JoseSerializer.DecodeAsync(token, remoteKey, CancellationToken.None)
        );
    }

    [Fact]
    public async Task DecodeAsync_JsonSerializationToARemoteKey_DecryptsThroughTheService()
    {
        SetupDecrypt();
        var remoteKey = CreateRemoteRsaKey();
        var token = await EncodeFlattenedJsonAsync(remoteKey, CancellationToken.None);

        var decoded = await JoseSerializer.DecodeAsync(token, remoteKey, CancellationToken.None);

        Assert.Equal(Payload, decoded);
    }

    [Fact]
    public async Task DecodeAsync_RsaOaep256EncryptedKeyNotTheModulusSize_FailsAtTheTagWithoutCallingTheService()
    {
        var remoteKey = CreateRemoteRsaKey();
        var token = await EncodeCompactAsync(
            remoteKey,
            AlgorithmCodes.KeyManagement.RsaOaep256,
            CancellationToken.None
        );
        var segments = token.Split('.');
        segments[1] = System.Buffers.Text.Base64Url.EncodeToString(new byte[255]);
        var tampered = string.Join('.', segments);

        await Assert.ThrowsAsync<JoseEncryptionException>(async () =>
            await JoseSerializer.DecodeAsync(tampered, remoteKey, CancellationToken.None)
        );
    }

    [Fact]
    public async Task DecodeAsync_JsonSerializationAndTheServiceFails_ThrowsTheServiceFailure()
    {
        Provider
            .Setup(x =>
                x.DecryptAsync(
                    It.IsAny<RemoteDecryptionRequest>(),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(() => ValueTask.FromException<int>(new IOException("unreachable")))
            .Verifiable(Times.Once());
        var remoteKey = CreateRemoteRsaKey();
        var token = await EncodeFlattenedJsonAsync(remoteKey, CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(async () =>
            await JoseSerializer.DecodeAsync(token, remoteKey, CancellationToken.None)
        );
    }

    #endregion
}
