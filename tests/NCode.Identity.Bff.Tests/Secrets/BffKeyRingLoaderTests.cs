// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Secrets;
using NCode.Identity.Jose;
using NCode.Identity.Registration;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Bff.Tests.Secrets;

public sealed class BffKeyRingLoaderTests : BaseTests, IAsyncLifetime
{
    private const string RingName = "ring";

    private sealed class StubBffOptionsAccessor(BffOptions current) : IBffOptionsAccessor
    {
        public BffOptions Current { get; } = current;
    }

    private DirectoryInfo KeysDirectory { get; } = Directory.CreateTempSubdirectory("bff-keys-");

    private FakeRemoteKeyProvider RemoteKeyProvider { get; } = new();

    private ServiceCollection ServiceCollection { get; } = new();

    private ServiceProvider? Provider { get; set; }

    public BffKeyRingLoaderTests()
    {
        ServiceCollection.AddLogging();
        ServiceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceCollection.AddSingleton<IRemoteKeyProvider>(RemoteKeyProvider);
        ServiceCollection.AddSingleton<IRemoteKeyDescriptorSource>(RemoteKeyProvider);
        ServiceCollection.AddSingleton<BffKeyRings>();
        ServiceCollection.AddSingleton<BffRemoteKeyReader>();
        ServiceCollection.AddSingleton<BffKeyRingLoader>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Provider is not null)
            await Provider.DisposeAsync();

        RemoteKeyProvider.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            KeysDirectory.Delete(true);

        base.Dispose(disposing);
    }

    private string WritePem(string fileName, string pem)
    {
        var path = Path.Combine(KeysDirectory.FullName, fileName);
        File.WriteAllText(path, pem, SecureEncoding.UTF8);
        return path;
    }

    private string WriteEcPem(string fileName)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return WritePem(fileName, key.ExportPkcs8PrivateKeyPem());
    }

    private string WriteRsaPem(string fileName)
    {
        using var key = RSA.Create(2048);
        return WritePem(fileName, key.ExportRSAPrivateKeyPem());
    }

    private static BffRemoteKeyOptions CreateSigningOptions() =>
        new()
        {
            Provider = FakeRemoteKeyProvider.ProviderName,
            KeyId = FakeRemoteKeyProvider.SigningKeyId,
            Algorithm = "ES256",
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
        };

    private void AddEnvironment(bool isRead)
    {
        if (isRead)
        {
            var mockEnvironment = CreateStrictMock<IHostEnvironment>();
            mockEnvironment
                .Setup(x => x.ContentRootPath)
                .Returns(KeysDirectory.FullName)
                .Verifiable();
            ServiceCollection.AddSingleton(mockEnvironment.Object);
        }
        else
        {
            ServiceCollection.AddSingleton(CreateStrictMock<IHostEnvironment>().Object);
        }
    }

    private ServiceProvider BuildProvider(BffKeyRingOptions keyRing)
    {
        AddEnvironment(keyRing.PemFiles is { Count: > 0 });

        var options = new BffOptions();
        options.KeyRings[RingName] = keyRing;
        ServiceCollection.AddSingleton<IBffOptionsAccessor>(new StubBffOptionsAccessor(options));

        Provider = ServiceCollection.BuildServiceProvider();
        return Provider;
    }

    private ServiceProvider BuildProvider(params string[] pemFiles) =>
        BuildProvider(new BffKeyRingOptions { PemFiles = [.. pemFiles] });

    private static async ValueTask<ISecretKeyCollectionProvider> LoadAsync(
        ServiceProvider provider,
        CancellationToken cancellationToken
    )
    {
        var loader = provider.GetRequiredService<BffKeyRingLoader>();
        await loader.StartingAsync(cancellationToken);
        return provider.GetRequiredService<BffKeyRings>().Get(RingName);
    }

    #region StartingAsync Tests

    [Fact]
    public async Task StartingAsync_WithAnEcPem_HoldsAKeyNamedAfterTheFile()
    {
        var path = WriteEcPem("signing-1.pem");
        var provider = BuildProvider(path);

        var keyRing = await LoadAsync(provider, CancellationToken.None);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("signing-1", key.KeyId);
        Assert.IsAssignableFrom<EccSecretKey>(key);
    }

    [Fact]
    public async Task StartingAsync_WithAnRsaPem_HoldsAKeyNamedAfterTheFile()
    {
        var path = WriteRsaPem("signing-2.key");
        var provider = BuildProvider(path);

        var keyRing = await LoadAsync(provider, CancellationToken.None);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("signing-2", key.KeyId);
        Assert.IsAssignableFrom<RsaSecretKey>(key);
    }

    [Fact]
    public async Task StartingAsync_WithASec1EcPem_HoldsAKeyNamedAfterTheFile()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var path = WritePem("sec1.pem", ecdsa.ExportECPrivateKeyPem());
        var provider = BuildProvider(path);

        var keyRing = await LoadAsync(provider, CancellationToken.None);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("sec1", key.KeyId);
        Assert.IsAssignableFrom<EccSecretKey>(key);
    }

    [Fact]
    public async Task StartingAsync_WithARelativePath_ResolvesItAgainstTheContentRoot()
    {
        WriteEcPem("relative.pem");
        var provider = BuildProvider("relative.pem");

        var keyRing = await LoadAsync(provider, CancellationToken.None);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("relative", key.KeyId);
    }

    [Fact]
    public async Task StartingAsync_WithAnAbsolutePath_ReadsTheFileAsGiven()
    {
        var otherDirectory = Directory.CreateTempSubdirectory("bff-other-keys-");
        try
        {
            var absolute = Path.Combine(otherDirectory.FullName, "absolute.pem");
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            File.WriteAllText(absolute, ecdsa.ExportPkcs8PrivateKeyPem(), SecureEncoding.UTF8);
            var provider = BuildProvider(absolute);

            var keyRing = await LoadAsync(provider, CancellationToken.None);

            var key = Assert.Single(keyRing.Collection);
            Assert.Equal("absolute", key.KeyId);
        }
        finally
        {
            otherDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task StartingAsync_WithSeveralFiles_HoldsEveryKey()
    {
        var provider = BuildProvider(WriteEcPem("first.pem"), WriteRsaPem("second.pem"));

        var keyRing = await LoadAsync(provider, CancellationToken.None);

        Assert.Equal(2, keyRing.Collection.Count);
        Assert.True(keyRing.Collection.TryGetByKeyId("first", out _));
        Assert.True(keyRing.Collection.TryGetByKeyId("second", out _));
    }

    [Fact]
    public async Task StartingAsync_WithAFileThatIsNoKey_ThrowsNamingTheKeyRing()
    {
        var path = WritePem("not-a-key.pem", "this is not a PEM file");
        var provider = BuildProvider(path);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await LoadAsync(provider, CancellationToken.None)
        );

        Assert.Equal(
            $"The key ring '{RingName}' cannot read '{path}': it holds no PEM-encoded EC or RSA private key.",
            exception.Message
        );
    }

    [Fact]
    public async Task StartingAsync_WithARemoteKey_HoldsItsPublicHalf()
    {
        var provider = BuildProvider(
            new BffKeyRingOptions { RemoteKeys = { ["client-sig"] = CreateSigningOptions() } }
        );

        var keyRing = await LoadAsync(provider, CancellationToken.None);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("client-sig", key.KeyId);
        Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Equal(SecretKeyUses.Signature, key.Metadata.Use);
        Assert.Equal(1, RemoteKeyProvider.SignatureCount);
    }

    [Fact]
    public async Task StartingAsync_WithPemFilesAndRemoteKeys_HoldsEveryKey()
    {
        var provider = BuildProvider(
            new BffKeyRingOptions
            {
                PemFiles = [WriteEcPem("local.pem")],
                RemoteKeys = { ["client-sig"] = CreateSigningOptions() },
            }
        );

        var keyRing = await LoadAsync(provider, CancellationToken.None);

        Assert.Equal(2, keyRing.Collection.Count);
        Assert.True(keyRing.Collection.TryGetByKeyId("local", out var local));
        Assert.IsAssignableFrom<EccSecretKey>(local);
        Assert.True(keyRing.Collection.TryGetByKeyId("client-sig", out var remote));
        Assert.IsAssignableFrom<IRemoteSecretKey>(remote);
    }

    [Fact]
    public async Task StartingAsync_WithARemoteKeyThatCannotBeRead_ThrowsNamingTheKeyRing()
    {
        var remoteKey = CreateSigningOptions();
        remoteKey.KeyId = "kms-missing";
        var provider = BuildProvider(
            new BffKeyRingOptions { RemoteKeys = { ["client-sig"] = remoteKey } }
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await LoadAsync(provider, CancellationToken.None)
        );

        Assert.Contains("'client-sig'", exception.Message);
        Assert.Contains($"'{RingName}'", exception.Message);
        Assert.IsType<CryptographicException>(exception.InnerException);
    }

    [Fact]
    public async Task StartingAsync_WithNullPemFilesAndARemoteKey_HoldsTheRemoteKey()
    {
        var provider = BuildProvider(
            new BffKeyRingOptions
            {
                PemFiles = null!,
                RemoteKeys = { ["client-sig"] = CreateSigningOptions() },
            }
        );

        var keyRing = await LoadAsync(provider, CancellationToken.None);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("client-sig", key.KeyId);
        Assert.IsAssignableFrom<IRemoteSecretKey>(key);
    }

    [Fact]
    public async Task StartingAsync_WithAPemFileAndNullRemoteKeys_HoldsThePemKey()
    {
        var provider = BuildProvider(
            new BffKeyRingOptions { PemFiles = [WriteEcPem("local.pem")], RemoteKeys = null! }
        );

        var keyRing = await LoadAsync(provider, CancellationToken.None);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("local", key.KeyId);
    }

    [Fact]
    public async Task StartingAsync_WhenALaterRemoteKeyFails_DisposesTheKeysReadBefore()
    {
        var mockResolver = CreateStrictMock<IRemoteKeyResolver>();
        var mockProver = CreateStrictMock<IRemoteKeyProver>();
        ServiceCollection.AddSingleton(mockResolver.Object);
        ServiceCollection.AddSingleton(mockProver.Object);

        var resolution = new RemoteKeyResolution
        {
            Provider = RemoteKeyProvider,
            Algorithm = RemoteKeyProvider.Algorithms.Single(row => row.Algorithm == "ES256"),
        };
        var readKey = new DisposableSecretKey(new KeyMetadata { KeyId = "a-read" });

        mockResolver
            .Setup(x => x.Resolve(It.IsAny<RemoteKeyAlgorithmSelector>()))
            .Returns(resolution)
            .Verifiable();
        mockResolver
            .Setup(x =>
                x.ReadKeyAsync(
                    It.IsAny<RemoteKeyBinding>(),
                    It.Is<KeyMetadata>(m => m.KeyId == "a-read"),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecretKey>(readKey))
            .Verifiable();
        mockResolver
            .Setup(x =>
                x.ReadKeyAsync(
                    It.IsAny<RemoteKeyBinding>(),
                    It.Is<KeyMetadata>(m => m.KeyId == "b-failing"),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
                ValueTask.FromException<SecretKey>(
                    new CryptographicException("The key is not held.")
                )
            )
            .Verifiable();
        mockProver
            .Setup(x => x.ProveAsync(readKey, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var provider = BuildProvider(
            new BffKeyRingOptions
            {
                RemoteKeys =
                {
                    ["a-read"] = CreateSigningOptions(),
                    ["b-failing"] = CreateSigningOptions(),
                },
            }
        );

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await LoadAsync(provider, CancellationToken.None)
        );

        Assert.True(readKey.IsDisposed);
        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<BffKeyRings>().Get(RingName)
        );
    }

    #endregion

    #region AddBffConfiguration Tests

    [Fact]
    public async Task AddBffConfiguration_WithAConfiguredKeyRing_RegistersItsKeyedProvider()
    {
        WriteEcPem("configured.pem");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{BffDefaults.ConfigurationSectionName}:KeyRings:{RingName}:PemFiles:0"] =
                        "configured.pem",
                }
            )
            .Build();
        ServiceCollection.NewBuilder<BffLibrary>().AddBffConfiguration(configuration);
        ServiceCollection.AddSingleton<IBffOptionsAccessor>(
            serviceProvider => new StubBffOptionsAccessor(
                serviceProvider.GetRequiredService<IOptions<BffOptions>>().Value
            )
        );
        AddEnvironment(isRead: true);
        Provider = ServiceCollection.BuildServiceProvider();

        var loader = Provider.GetServices<IHostedService>().OfType<BffKeyRingLoader>().Single();
        await loader.StartingAsync(CancellationToken.None);
        var keyRing = Provider.GetRequiredKeyedService<ISecretKeyCollectionProvider>(RingName);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("configured", key.KeyId);
    }

    #endregion
}
