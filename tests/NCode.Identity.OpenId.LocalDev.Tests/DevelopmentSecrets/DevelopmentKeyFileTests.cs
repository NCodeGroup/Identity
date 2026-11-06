// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.OpenId.LocalDev.DevelopmentSecrets;

namespace NCode.Identity.OpenId.LocalDev.Tests.DevelopmentSecrets;

public class DevelopmentKeyFileTests : IDisposable
{
    private const string FileName = "mgmt-client.pem";

    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    #region EnsureEcPrivateKey Tests

    [Fact]
    public void EnsureEcPrivateKey_WhenDirectoryIsMissing_CreatesItAndTheFile()
    {
        var directory = Path.Combine(_root, "nested", "LocalDev");

        var path = DevelopmentKeyFile.EnsureEcPrivateKey(directory, FileName);

        Assert.Equal(Path.Combine(directory, FileName), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void EnsureEcPrivateKey_WhenFileIsMissing_WritesAP256Pkcs8Key()
    {
        var path = DevelopmentKeyFile.EnsureEcPrivateKey(_root, FileName);

        var pem = File.ReadAllText(path, SecureEncoding.UTF8);
        using var key = ECDsa.Create();
        key.ImportFromPem(pem);

        Assert.StartsWith("-----BEGIN PRIVATE KEY-----", pem, StringComparison.Ordinal);
        Assert.Equal(256, key.KeySize);
    }

    [Fact]
    public void EnsureEcPrivateKey_WhenCalledTwice_ReusesTheKey()
    {
        var firstPath = DevelopmentKeyFile.EnsureEcPrivateKey(_root, FileName);
        var firstPem = File.ReadAllText(firstPath, SecureEncoding.UTF8);

        var secondPath = DevelopmentKeyFile.EnsureEcPrivateKey(_root, FileName);
        var secondPem = File.ReadAllText(secondPath, SecureEncoding.UTF8);

        Assert.Equal(firstPath, secondPath);
        Assert.Equal(firstPem, secondPem);
    }

    [Fact]
    public void EnsureEcPrivateKey_WhenFileIsEmpty_ReplacesIt()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, FileName), string.Empty, SecureEncoding.UTF8);

        var path = DevelopmentKeyFile.EnsureEcPrivateKey(_root, FileName);

        var pem = File.ReadAllText(path, SecureEncoding.UTF8);
        using var key = ECDsa.Create();
        key.ImportFromPem(pem);
        Assert.Equal(256, key.KeySize);
    }

    [Fact]
    public void EnsureEcPrivateKey_Always_LeavesNoTemporaryFile()
    {
        DevelopmentKeyFile.EnsureEcPrivateKey(_root, FileName);

        var files = Directory.GetFiles(_root);

        Assert.Single(files);
    }

    #endregion

    #region ReadPublicJwks Tests

    [Fact]
    public void ReadPublicJwks_Always_ReturnsOneEs256SigningKeyNamedAfterTheFileStem()
    {
        var path = DevelopmentKeyFile.EnsureEcPrivateKey(_root, FileName);

        var jwks = DevelopmentKeyFile.ReadPublicJwks(path);

        var root = JsonElement.Parse(jwks);
        var keys = root.GetProperty("keys");
        Assert.Equal(1, keys.GetArrayLength());
        var jwk = keys[0];
        Assert.Equal("EC", jwk.GetProperty("kty").GetString());
        Assert.Equal("P-256", jwk.GetProperty("crv").GetString());
        Assert.Equal("mgmt-client", jwk.GetProperty("kid").GetString());
        Assert.Equal("sig", jwk.GetProperty("use").GetString());
        Assert.Equal("ES256", jwk.GetProperty("alg").GetString());
    }

    [Fact]
    public void ReadPublicJwks_Always_ReturnsTheCoordinatesOfThePublicKey()
    {
        var path = DevelopmentKeyFile.EnsureEcPrivateKey(_root, FileName);
        using var key = ECDsa.Create();
        key.ImportFromPem(File.ReadAllText(path, SecureEncoding.UTF8));
        var expected = key.ExportParameters(includePrivateParameters: false);

        var jwks = DevelopmentKeyFile.ReadPublicJwks(path);

        var jwk = JsonElement.Parse(jwks).GetProperty("keys")[0];
        Assert.Equal(expected.Q.X, Base64Url.DecodeFromChars(jwk.GetProperty("x").GetString()));
        Assert.Equal(expected.Q.Y, Base64Url.DecodeFromChars(jwk.GetProperty("y").GetString()));
    }

    [Fact]
    public void ReadPublicJwks_Always_HoldsNoPrivateMember()
    {
        var path = DevelopmentKeyFile.EnsureEcPrivateKey(_root, FileName);

        var jwks = DevelopmentKeyFile.ReadPublicJwks(path);

        var jwk = JsonElement.Parse(jwks).GetProperty("keys")[0];
        Assert.False(jwk.TryGetProperty("d", out _));
    }

    #endregion
}
