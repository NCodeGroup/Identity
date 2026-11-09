// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Jose.Tests;

// See ADR-1472.
public class RemoteKeyJwsTests : BaseTests
{
    private const string Payload = """{"foo":"bar","n":123}""";
    private const string RemoteKeyId = "remote-key-1";
    private const string RemoteKid = "remote-kid";
    private const string LocalKid = "local-kid";

    private static RemoteSigningAlgorithm Ps256Row { get; } =
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

    private static RemoteSigningAlgorithm Rs256Row { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RS256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            SigningAlgorithm = "RSASSA_PKCS1_V1_5_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
        };

    private static RemoteSigningAlgorithm Es256Row { get; } =
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

    private ServiceProvider ServiceProvider { get; }

    private IJoseSerializer JoseSerializer { get; }

    private IAlgorithmCollection AlgorithmCollection { get; }

    private Mock<IRemoteKeyProvider> Provider { get; }

    private RSA HeldRsa { get; } = RSA.Create(2048);

    private ECDsa HeldEcdsa { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private RSA LocalRsa { get; } = RSA.Create(2048);

    public RemoteKeyJwsTests()
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
            HeldEcdsa.Dispose();
            LocalRsa.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Helpers

    private SignatureAlgorithm GetSignatureAlgorithm(string code)
    {
        Assert.True(AlgorithmCollection.TryGetSignatureAlgorithm(code, out var algorithm));
        return algorithm;
    }

    // The rows set a digest algorithm, so the request carries the digest the key management service signs.
    private byte[] SignAsProvider(RemoteSignatureRequest request) =>
        request.Algorithm.Algorithm switch
        {
            "PS256" => HeldRsa.SignHash(
                request.Data.Span,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss
            ),
            "RS256" => HeldRsa.SignHash(
                request.Data.Span,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            ),
            "ES256" => HeldEcdsa.SignHash(request.Data.Span, DSASignatureFormat.Rfc3279DerSequence),
            _ => throw new InvalidOperationException(),
        };

    private void SetupProvider() =>
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
                    var bytes = SignAsProvider(request);
                    bytes.CopyTo(destination);
                    return ValueTask.FromResult(bytes.Length);
                }
            )
            .Verifiable();

    private RemoteKeyBinding CreateBinding(RemoteSigningAlgorithm row) =>
        new()
        {
            Provider = Provider.Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, RemoteKeyId),
            Algorithm = row,
        };

    private SecretKey CreateRemoteKey(string algorithmCode)
    {
        var metadata = new KeyMetadata { KeyId = RemoteKid };
        return algorithmCode switch
        {
            "PS256" => new RemoteRsaSecretKey(
                metadata,
                2048,
                HeldRsa.ExportSubjectPublicKeyInfo(),
                CreateBinding(Ps256Row)
            ),
            "RS256" => new RemoteRsaSecretKey(
                metadata,
                2048,
                HeldRsa.ExportSubjectPublicKeyInfo(),
                CreateBinding(Rs256Row)
            ),
            "ES256" => new RemoteEccSecretKey(
                metadata,
                EccCurveSpecification.P256.CurveName,
                256,
                HeldEcdsa.ExportSubjectPublicKeyInfo(),
                CreateBinding(Es256Row)
            ),
            _ => throw new InvalidOperationException(),
        };
    }

    private SecretKey CreatePublicKey(string algorithmCode)
    {
        var metadata = new KeyMetadata { KeyId = RemoteKid };
        return algorithmCode == "ES256"
            ? SecretKeyFactory.CreateEccSpki(metadata, HeldEcdsa.ExportSubjectPublicKeyInfo())
            : SecretKeyFactory.CreateRsaSpki(metadata, HeldRsa.ExportSubjectPublicKeyInfo());
    }

    #endregion

    #region EncodeAsync Tests

    [Theory]
    [InlineData("PS256", JoseSerializationFormat.Compact)]
    [InlineData("PS256", JoseSerializationFormat.Flattened)]
    [InlineData("PS256", JoseSerializationFormat.General)]
    [InlineData("RS256", JoseSerializationFormat.Compact)]
    [InlineData("RS256", JoseSerializationFormat.Flattened)]
    [InlineData("RS256", JoseSerializationFormat.General)]
    [InlineData("ES256", JoseSerializationFormat.Compact)]
    [InlineData("ES256", JoseSerializationFormat.Flattened)]
    [InlineData("ES256", JoseSerializationFormat.General)]
    public async Task EncodeAsync_WhenRemoteKeySigns_DecodesWithThePublicKey(
        string algorithmCode,
        JoseSerializationFormat format
    )
    {
        SetupProvider();
        var credentials = new JoseSigningCredentials(
            CreateRemoteKey(algorithmCode),
            GetSignatureAlgorithm(algorithmCode)
        );
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = format,
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var parsed = JoseSerializer.Parse(token);
        var decoded = await JoseSerializer.DecodeAsync(
            parsed,
            CreatePublicKey(algorithmCode),
            CancellationToken.None
        );
        Assert.Equal(Payload, decoded);
        Assert.Equal(algorithmCode, parsed.Header.GetProperty("alg").GetString());
    }

    [Fact]
    public async Task EncodeAsync_WhenGeneralJwsMixesLocalAndRemoteSigners_VerifiesBoth()
    {
        SetupProvider();
        var localKey = SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = LocalKid }, LocalRsa);
        var localPublicKey = SecretKeyFactory.CreateRsaSpki(
            new KeyMetadata { KeyId = LocalKid },
            LocalRsa.ExportSubjectPublicKeyInfo()
        );
        var options = new JoseSigningOptions
        {
            Format = JoseSerializationFormat.General,
            Signatures =
            [
                new JoseSignatureOptions(
                    new JoseSigningCredentials(localKey, GetSignatureAlgorithm("RS256"))
                ),
                new JoseSignatureOptions(
                    new JoseSigningCredentials(
                        CreateRemoteKey("ES256"),
                        GetSignatureAlgorithm("ES256")
                    )
                ),
            ],
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var signatures = JsonElement.Parse(token).GetProperty("signatures");
        Assert.Equal(2, signatures.GetArrayLength());
        Assert.Equal(
            Payload,
            await JoseSerializer.DecodeAsync(token, localPublicKey, CancellationToken.None)
        );
        Assert.Equal(
            Payload,
            await JoseSerializer.DecodeAsync(
                token,
                CreatePublicKey("ES256"),
                CancellationToken.None
            )
        );
    }

    #endregion
}
