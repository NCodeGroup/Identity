// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

public sealed class DefaultRemoteSecretImporterTests : IAsyncLifetime
{
    private const string KeyId = "key-1";

    private ServiceProvider ServiceProvider { get; }
    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IClientKeyImporter> MockKeyImporter { get; }
    private StubKmsProvider Provider { get; } = new();
    private DefaultRemoteSecretImporter Importer { get; }

    public DefaultRemoteSecretImporterTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddPersistenceLibrary();
        secrets.AddJoseLibrary();
        serviceCollection.AddSingleton<IRemoteKeyProvider>(Provider);
        serviceCollection.AddSingleton<IRemoteSigningAlgorithmSource>(Provider);
        ServiceProvider = serviceCollection.BuildServiceProvider();

        MockKeyImporter = Mocks.Create<IClientKeyImporter>();
        Importer = new DefaultRemoteSecretImporter(
            MockKeyImporter.Object,
            ServiceProvider.GetRequiredService<ISecretSerializer>(),
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            NullLogger<DefaultRemoteSecretImporter>.Instance
        );
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        Mocks.Verify();
        await ServiceProvider.DisposeAsync();
        Provider.Dispose();
    }

    private static string EncodeSubjectPublicKeyInfo(ECDsa key) =>
        Base64Url.EncodeToString(key.ExportSubjectPublicKeyInfo());

    private static PersistedSecret CreatePublicSecret(ECDsa key) =>
        new PersistedSecretBuilder
        {
            SecretType = SecretTypes.Ecc,
            SecretEncoding = SecretEncodings.Spki,
            EncodedValue = EncodeSubjectPublicKeyInfo(key),
            KeySizeBits = 256,
            Algorithm = "ES256",
            Use = "sig",
        }.Build();

    private static PersistedSecret CreateRemoteSecret(
        ECDsa publicKey,
        string reference,
        int keySizeBits,
        string algorithm
    ) =>
        new PersistedSecretBuilder
        {
            SecretType = SecretTypes.Ecc,
            SecretEncoding = SecretEncodings.Remote,
            EncodedValue = reference,
            PublicKey = EncodeSubjectPublicKeyInfo(publicKey),
            KeySizeBits = keySizeBits,
            Algorithm = algorithm,
            Use = "sig",
        }.Build();

    private static RemoteSecretImportRequest CreateImportRequest(ECDsa key) =>
        new()
        {
            Key = new SecretKeyImportRequest
            {
                SecretId = "secret-1",
                SecretType = SecretTypes.Ecc,
                Format = SecretKeyImportFormats.Pem,
                KeyMaterial = key.ExportSubjectPublicKeyInfoPem(),
                Use = "sig",
                Algorithm = "ES256",
                CreatedWhen = DateTimeOffset.UnixEpoch,
                ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            },
            Reference = new RemoteKeyReference(StubKmsProvider.ProviderName, KeyId),
        };

    #region TryImport Tests

    [Fact]
    public void TryImport_WhenTheKeyImports_ReturnsASuppliedRemoteSecretThatNamesTheKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = CreateImportRequest(key);
        PersistedSecret? publicSecret = CreatePublicSecret(key);
        string? importError = null;
        MockKeyImporter
            .Setup(x => x.TryImportKey(request.Key, out publicSecret, out importError))
            .Returns(true)
            .Verifiable();

        var result = Importer.TryImport(request, out var secret, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.NotNull(secret);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal("stub-kms:key-1", secret.EncodedValue);
        Assert.Equal(EncodeSubjectPublicKeyInfo(key), secret.PublicKey);
        Assert.Equal(SecretOrigins.Supplied, secret.Origin);
        Assert.Null(secret.ManagedBy);
    }

    [Fact]
    public void TryImport_WhenTheImporterRefuses_ReturnsItsError()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = CreateImportRequest(key);
        PersistedSecret? publicSecret = null;
        string? importError = "The key is refused.";
        MockKeyImporter
            .Setup(x => x.TryImportKey(request.Key, out publicSecret, out importError))
            .Returns(false)
            .Verifiable();

        var result = Importer.TryImport(request, out var secret, out var error);

        Assert.False(result);
        Assert.Null(secret);
        Assert.Equal("The key is refused.", error);
    }

    #endregion

    #region VerifyBindingAsync Tests

    [Fact]
    public async Task VerifyBindingAsync_WhenTheKeySignsWithThePublicKey_ReturnsNull()
    {
        var secret = CreateRemoteSecret(
            Provider.Key,
            $"{StubKmsProvider.ProviderName}:{KeyId}",
            256,
            "ES256"
        );

        var result = await Importer.VerifyBindingAsync(secret, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyBindingAsync_WhenThePublicKeyIsAnotherKeys_ReturnsBadRequest()
    {
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var secret = CreateRemoteSecret(
            otherKey,
            $"{StubKmsProvider.ProviderName}:{KeyId}",
            256,
            "ES256"
        );

        var result = await Importer.VerifyBindingAsync(secret, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(
            "The key 'key-1' of the remote key provider 'stub-kms' does not sign with the private half of the supplied public key.",
            result.Detail
        );
    }

    [Fact]
    public async Task VerifyBindingAsync_WhenTheProviderIsNotRegistered_ReturnsBadRequest()
    {
        var secret = CreateRemoteSecret(Provider.Key, $"other-kms:{KeyId}", 256, "ES256");

        var result = await Importer.VerifyBindingAsync(secret, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(
            $"The secret '{secret.SecretId}' names the remote key provider 'other-kms', which is not registered.",
            result.Detail
        );
    }

    [Fact]
    public async Task VerifyBindingAsync_WhenNoRowMatches_ReturnsBadRequest()
    {
        using var p384Key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var secret = CreateRemoteSecret(
            p384Key,
            $"{StubKmsProvider.ProviderName}:{KeyId}",
            384,
            "ES384"
        );

        var result = await Importer.VerifyBindingAsync(secret, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(
            $"The secret '{secret.SecretId}' has no remote signing algorithm for provider 'stub-kms', algorithm 'ES384', secret type '{SecretTypes.Ecc}', key size 384 and curve 'P-384'.",
            result.Detail
        );
    }

    [Fact]
    public async Task VerifyBindingAsync_WhenTheProviderFails_ReturnsBadGateway()
    {
        Provider.Failure = new IOException();
        var secret = CreateRemoteSecret(
            Provider.Key,
            $"{StubKmsProvider.ProviderName}:{KeyId}",
            256,
            "ES256"
        );

        var result = await Importer.VerifyBindingAsync(secret, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal(
            "The key management service of the remote key provider 'stub-kms' did not sign with the key 'key-1': it refused the key or the deployment's identity, or it could not be reached.",
            result.Detail
        );
    }

    #endregion

    private sealed class StubKmsProvider
        : IRemoteKeyProvider,
            IRemoteSigningAlgorithmSource,
            IDisposable
    {
        public const string ProviderName = "stub-kms";

        private static readonly IReadOnlyList<RemoteSigningAlgorithm> Rows =
        [
            new RemoteSigningAlgorithm
            {
                Provider = ProviderName,
                Algorithm = "ES256",
                SecretType = SecretKeyTypes.Ecc,
                KeySizeBits = EccCurveSpecification.P256.CurveSizeBits,
                Curve = EccCurveSpecification.P256.CurveName,
                KeySpec = "ECC_NIST_P256",
                SigningAlgorithm = "ECDSA_SHA_256",
                DigestAlgorithm = HashAlgorithmName.SHA256,
                IsDerEncodedSignature = true,
            },
        ];

        public ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        public Exception? Failure { get; set; }

        public string Name => ProviderName;

        public IEnumerable<RemoteSigningAlgorithm> Algorithms => Rows;

        public ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
            RemoteKeyCreationRequest request,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<byte[]> SignAsync(
            RemoteSignatureRequest request,
            CancellationToken cancellationToken
        )
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            if (!string.Equals(request.KeyId, KeyId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The key '{request.KeyId}' does not exist.");
            }

            return ValueTask.FromResult(
                Key.SignHash(request.Data.Span, DSASignatureFormat.Rfc3279DerSequence)
            );
        }

        public ValueTask DestroyKeyAsync(string keyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Dispose() => Key.Dispose();
    }
}
