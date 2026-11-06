// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Buffers;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Secrets;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Bff.Tests.Secrets;

public sealed class BffPemKeyRingTests : BaseTests, IAsyncLifetime
{
    private const string RingName = "ring";

    private DirectoryInfo KeysDirectory { get; } = Directory.CreateTempSubdirectory("bff-keys-");

    private ServiceCollection ServiceCollection { get; } = new();

    private ServiceProvider? Provider { get; set; }

    public BffPemKeyRingTests()
    {
        ServiceCollection.AddLogging();
        ServiceCollection.AddSecretsLibrary();

        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.ContentRootPath).Returns(KeysDirectory.FullName).Verifiable();
        ServiceCollection.AddSingleton(mockEnvironment.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Provider is not null)
            await Provider.DisposeAsync();
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

    private ServiceProvider BuildProvider(params string[] pemFiles)
    {
        var options = new BffOptions();
        options.KeyRings[RingName] = new BffKeyRingOptions { PemFiles = [.. pemFiles] };
        ServiceCollection.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));

        Provider = ServiceCollection.BuildServiceProvider();
        return Provider;
    }

    #region Load Tests

    [Fact]
    public void Load_WithAnEcPem_HoldsAKeyNamedAfterTheFile()
    {
        var path = WriteEcPem("signing-1.pem");
        var provider = BuildProvider(path);

        var keyRing = BffPemKeyRing.Load(provider, RingName);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("signing-1", key.KeyId);
        Assert.IsAssignableFrom<EccSecretKey>(key);
    }

    [Fact]
    public void Load_WithAnRsaPem_HoldsAKeyNamedAfterTheFile()
    {
        var path = WriteRsaPem("signing-2.key");
        var provider = BuildProvider(path);

        var keyRing = BffPemKeyRing.Load(provider, RingName);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("signing-2", key.KeyId);
        Assert.IsAssignableFrom<RsaSecretKey>(key);
    }

    [Fact]
    public void Load_WithASec1EcPem_HoldsAKeyNamedAfterTheFile()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var path = WritePem("sec1.pem", ecdsa.ExportECPrivateKeyPem());
        var provider = BuildProvider(path);

        var keyRing = BffPemKeyRing.Load(provider, RingName);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("sec1", key.KeyId);
        Assert.IsAssignableFrom<EccSecretKey>(key);
    }

    [Fact]
    public void Load_WithARelativePath_ResolvesItAgainstTheContentRoot()
    {
        WriteEcPem("relative.pem");
        var provider = BuildProvider("relative.pem");

        var keyRing = BffPemKeyRing.Load(provider, RingName);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("relative", key.KeyId);
    }

    [Fact]
    public void Load_WithAnAbsolutePath_ReadsTheFileAsGiven()
    {
        var otherDirectory = Directory.CreateTempSubdirectory("bff-other-keys-");
        try
        {
            var absolute = Path.Combine(otherDirectory.FullName, "absolute.pem");
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            File.WriteAllText(absolute, ecdsa.ExportPkcs8PrivateKeyPem(), SecureEncoding.UTF8);
            var provider = BuildProvider(absolute);

            var keyRing = BffPemKeyRing.Load(provider, RingName);

            var key = Assert.Single(keyRing.Collection);
            Assert.Equal("absolute", key.KeyId);
        }
        finally
        {
            otherDirectory.Delete(true);
        }
    }

    [Fact]
    public void Load_WithSeveralFiles_HoldsEveryKey()
    {
        var provider = BuildProvider(WriteEcPem("first.pem"), WriteRsaPem("second.pem"));

        var keyRing = BffPemKeyRing.Load(provider, RingName);

        Assert.Equal(2, keyRing.Collection.Count);
        Assert.True(keyRing.Collection.TryGetByKeyId("first", out _));
        Assert.True(keyRing.Collection.TryGetByKeyId("second", out _));
    }

    [Fact]
    public void Load_WithAFileThatIsNoKey_ThrowsNamingTheKeyRing()
    {
        var path = WritePem("not-a-key.pem", "this is not a PEM file");
        var provider = BuildProvider(path);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            BffPemKeyRing.Load(provider, RingName)
        );

        Assert.Equal(
            $"The key ring '{RingName}' cannot read '{path}': it holds no PEM-encoded EC or RSA private key.",
            exception.Message
        );
    }

    #endregion

    #region AddBffConfiguration Tests

    [Fact]
    public void AddBffConfiguration_WithAConfiguredKeyRing_RegistersItsKeyedProvider()
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
        Provider = ServiceCollection.BuildServiceProvider();

        var keyRing = Provider.GetRequiredKeyedService<ISecretKeyCollectionProvider>(RingName);

        var key = Assert.Single(keyRing.Collection);
        Assert.Equal("configured", key.KeyId);
    }

    #endregion
}
