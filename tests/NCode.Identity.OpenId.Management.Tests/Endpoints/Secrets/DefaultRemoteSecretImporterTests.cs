// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

public sealed class DefaultRemoteSecretImporterTests : IAsyncLifetime
{
    private const string KeyId = "key-1";

    private ServiceProvider ServiceProvider { get; }
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
        serviceCollection.AddSingleton<IRemoteKeyDescriptorSource>(Provider);
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Importer = new DefaultRemoteSecretImporter(
            ServiceProvider.GetRequiredService<ISecretGenerator>(),
            ServiceProvider.GetRequiredService<ISecretSerializer>(),
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            NullLogger<DefaultRemoteSecretImporter>.Instance
        );
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
        Provider.Dispose();
    }

    private static string EncodeSubjectPublicKeyInfo(ECDsa key) =>
        Base64Url.EncodeToString(key.ExportSubjectPublicKeyInfo());

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

    private static BindRemoteSecretRequest CreateBindRequest(
        string keyId = KeyId,
        string algorithm = "ES256"
    ) =>
        new()
        {
            SecretId = "secret-1",
            Provider = StubKmsProvider.ProviderName,
            KeyId = keyId,
            SecretType = SecretTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
            Use = "sig",
            Algorithm = algorithm,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    #region ImportAsync Tests

    [Fact]
    public async Task ImportAsync_WhenTheServiceHoldsTheKey_ReturnsASuppliedRemoteSecretWithTheServicesPublicKey()
    {
        var result = await Importer.ImportAsync(CreateBindRequest(), CancellationToken.None);

        Assert.True(result.IsBound);
        Assert.Null(result.Error);
        var secret = result.Secret;
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal("stub-kms:key-1", secret.EncodedValue);
        Assert.Equal(EncodeSubjectPublicKeyInfo(Provider.Key), secret.PublicKey);
        Assert.Equal(SecretOrigins.Supplied, secret.Origin);
        Assert.Null(secret.ManagedBy);
    }

    [Fact]
    public async Task ImportAsync_WhenTheServiceHoldsNoSuchKey_ReturnsBadRequest()
    {
        var result = await Importer.ImportAsync(
            CreateBindRequest(keyId: "missing"),
            CancellationToken.None
        );

        Assert.False(result.IsBound);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
        Assert.Contains("'missing'", result.Error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_WhenNoRowMatches_ReturnsBadRequest()
    {
        var result = await Importer.ImportAsync(
            CreateBindRequest(algorithm: "ES384"),
            CancellationToken.None
        );

        Assert.False(result.IsBound);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
    }

    [Fact]
    public async Task ImportAsync_WhenTheProviderRefusesTheKey_ReturnsBadRequest()
    {
        Provider.PublicKeyFailure = new CryptographicException("not a signing key");

        var result = await Importer.ImportAsync(CreateBindRequest(), CancellationToken.None);

        Assert.False(result.IsBound);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
        Assert.Contains("not a signing key", result.Error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_WhenTheServiceFails_ReturnsBadGateway()
    {
        Provider.PublicKeyFailure = new IOException("unreachable");

        var result = await Importer.ImportAsync(CreateBindRequest(), CancellationToken.None);

        Assert.False(result.IsBound);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status502BadGateway, result.Error.StatusCode);
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
    public async Task VerifyBindingAsync_WhenTheServiceSignsWithAnotherKey_ReturnsBadRequest()
    {
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Provider.SigningKey = otherKey;
        var secret = CreateRemoteSecret(
            Provider.Key,
            $"{StubKmsProvider.ProviderName}:{KeyId}",
            256,
            "ES256"
        );

        var result = await Importer.VerifyBindingAsync(secret, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(
            "The key 'key-1' of the remote key provider 'stub-kms' does not sign with the private half of the public key the service returned.",
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
            IRemoteKeyDescriptorSource,
            IDisposable
    {
        public const string ProviderName = "stub-kms";

        private static IReadOnlyList<RemoteSigningAlgorithm> Rows { get; } =
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

        public Exception? PublicKeyFailure { get; set; }

        public ECDsa? SigningKey { get; set; }

        public string Name => ProviderName;

        public IEnumerable<RemoteKeyProviderDescriptor> Providers { get; } =
        [new RemoteKeyProviderDescriptor { Name = ProviderName }];

        public IEnumerable<RemoteSigningAlgorithm> Algorithms => Rows;

        public ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
            RemoteKeyCreationRequest request,
            CancellationToken cancellationToken
        ) => ValueTask.FromException<RemoteKeyCreationResult>(new NotSupportedException());

        public ValueTask GetPublicKeyAsync(
            RemotePublicKeyRequest request,
            IBufferWriter<byte> subjectPublicKeyInfo,
            CancellationToken cancellationToken
        )
        {
            if (PublicKeyFailure is not null)
            {
                return ValueTask.FromException(PublicKeyFailure);
            }

            if (!string.Equals(request.KeyId, KeyId, StringComparison.Ordinal))
            {
                return ValueTask.FromException(
                    new InvalidOperationException($"The key '{request.KeyId}' does not exist.")
                );
            }

            subjectPublicKeyInfo.Write(Key.ExportSubjectPublicKeyInfo());
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> SignAsync(
            RemoteSignatureRequest request,
            Memory<byte> signature,
            CancellationToken cancellationToken
        )
        {
            if (Failure is not null)
            {
                return ValueTask.FromException<int>(Failure);
            }

            if (!string.Equals(request.KeyId, KeyId, StringComparison.Ordinal))
            {
                return ValueTask.FromException<int>(
                    new InvalidOperationException($"The key '{request.KeyId}' does not exist.")
                );
            }

            return (SigningKey ?? Key).TrySignHash(
                request.Data.Span,
                signature.Span,
                DSASignatureFormat.Rfc3279DerSequence,
                out var written
            )
                ? ValueTask.FromResult(written)
                : ValueTask.FromException<int>(
                    new CryptographicException("The signature does not fit in the buffer.")
                );
        }

        public ValueTask DestroyKeyAsync(string keyId, CancellationToken cancellationToken) =>
            ValueTask.FromException(new NotSupportedException());

        public void Dispose() => Key.Dispose();
    }
}
