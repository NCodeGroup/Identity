#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Encoders;
using NCode.Identity.OpenId.Client.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Keys;

public class ProviderKeySetImporterTests : BaseTests, IAsyncLifetime
{
    private ServiceProvider ServiceProvider { get; } = CreateServiceProvider();

    private static ServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary();
        return serviceCollection.BuildServiceProvider();
    }

    private ProviderKeySetImporter CreateImporter() =>
        new(ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>());

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await ServiceProvider.DisposeAsync();

    #region TryImport Tests

    [Fact]
    public void TryImport_WithTwoRsaKeys_ImportsBothWithKeyIds()
    {
        using var rsa1 = RSA.Create(2048);
        using var rsa2 = RSA.Create(2048);
        var json = TestJwks.Set(TestJwks.Rsa(rsa1, "k1"), TestJwks.Rsa(rsa2, "k2"));

        var result = CreateImporter().TryImport(Parse(json), out var keys, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.NotNull(keys);
        Assert.Equal(["k1", "k2"], keys.Select(key => key.KeyId));
    }

    [Fact]
    public void TryImport_WithEncryptionKey_SkipsItAndImportsTheOther()
    {
        using var rsa1 = RSA.Create(2048);
        using var rsa2 = RSA.Create(2048);
        var json = TestJwks.Set(TestJwks.Rsa(rsa1, "enc", "enc"), TestJwks.Rsa(rsa2, "sig1"));

        var result = CreateImporter().TryImport(Parse(json), out var keys, out _);

        Assert.True(result);
        Assert.NotNull(keys);
        Assert.Equal("sig1", Assert.Single(keys).KeyId);
    }

    [Fact]
    public void TryImport_WithPrivateMember_RefusesTheSet()
    {
        using var rsa1 = RSA.Create(2048);
        using var rsa2 = RSA.Create(2048);
        var privateKey = TestJwks.Rsa(rsa1, "k1");
        privateKey["d"] = Base64Url.Encode(rsa1.ExportParameters(true).D!);
        var json = TestJwks.Set(TestJwks.Rsa(rsa2, "k2"), privateKey);

        var result = CreateImporter().TryImport(Parse(json), out var keys, out var error);

        Assert.False(result);
        Assert.Null(keys);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryImport_WithSymmetricKey_RefusesTheSet()
    {
        using var rsa = RSA.Create(2048);
        var json = TestJwks.Set(
            TestJwks.Rsa(rsa, "k1"),
            new Dictionary<string, object>
            {
                ["kty"] = "oct",
                ["k"] = "AAAA",
                ["kid"] = "s",
            }
        );

        var result = CreateImporter().TryImport(Parse(json), out var keys, out var error);

        Assert.False(result);
        Assert.Null(keys);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryImport_WithDuplicateKeyId_RefusesTheSet()
    {
        using var rsa1 = RSA.Create(2048);
        using var rsa2 = RSA.Create(2048);
        var json = TestJwks.Set(TestJwks.Rsa(rsa1, "dup"), TestJwks.Rsa(rsa2, "dup"));

        var result = CreateImporter().TryImport(Parse(json), out var keys, out var error);

        Assert.False(result);
        Assert.Null(keys);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryImport_WithUnknownKeyType_SkipsIt()
    {
        using var rsa = RSA.Create(2048);
        var json = TestJwks.Set(
            new Dictionary<string, object> { ["kty"] = "XYZ" },
            TestJwks.Rsa(rsa, "k1")
        );

        var result = CreateImporter().TryImport(Parse(json), out var keys, out _);

        Assert.True(result);
        Assert.NotNull(keys);
        Assert.Equal("k1", Assert.Single(keys).KeyId);
    }

    [Fact]
    public void TryImport_WithNonObjectElement_SkipsIt()
    {
        using var rsa = RSA.Create(2048);
        var json = TestJwks.Set(TestJwks.Rsa(rsa, "k1")).Replace("\"keys\":[", "\"keys\":[1,");

        var result = CreateImporter().TryImport(Parse(json), out var keys, out _);

        Assert.True(result);
        Assert.NotNull(keys);
        Assert.Equal("k1", Assert.Single(keys).KeyId);
    }

    [Fact]
    public void TryImport_WithKeyWithoutKeyType_SkipsIt()
    {
        using var rsa = RSA.Create(2048);
        var json = TestJwks.Set(
            new Dictionary<string, object> { ["kid"] = "nokty" },
            TestJwks.Rsa(rsa, "k1")
        );

        var result = CreateImporter().TryImport(Parse(json), out var keys, out _);

        Assert.True(result);
        Assert.NotNull(keys);
        Assert.Equal("k1", Assert.Single(keys).KeyId);
    }

    [Fact]
    public void TryImport_WithKeyOperationsWithoutVerify_SkipsIt()
    {
        using var rsa1 = RSA.Create(2048);
        using var rsa2 = RSA.Create(2048);
        var signOnly = TestJwks.Rsa(rsa1, "signonly");
        signOnly["key_ops"] = new[] { "sign" };
        var json = TestJwks.Set(signOnly, TestJwks.Rsa(rsa2, "k1"));

        var result = CreateImporter().TryImport(Parse(json), out var keys, out _);

        Assert.True(result);
        Assert.NotNull(keys);
        Assert.Equal("k1", Assert.Single(keys).KeyId);
    }

    [Fact]
    public void TryImport_WithMalformedKeyMaterial_SkipsIt()
    {
        using var rsa = RSA.Create(2048);
        var json = TestJwks.Set(
            new Dictionary<string, object>
            {
                ["kty"] = "RSA",
                ["kid"] = "bad",
                ["n"] = "!!!",
                ["e"] = "AQAB",
            },
            TestJwks.Rsa(rsa, "k1")
        );

        var result = CreateImporter().TryImport(Parse(json), out var keys, out _);

        Assert.True(result);
        Assert.NotNull(keys);
        Assert.Equal("k1", Assert.Single(keys).KeyId);
    }

    [Theory]
    [InlineData("""{"keys":[]}""")]
    [InlineData("[]")]
    public void TryImport_WithNoKeys_RefusesTheSet(string json)
    {
        var result = CreateImporter().TryImport(Parse(json), out var keys, out var error);

        Assert.False(result);
        Assert.Null(keys);
        Assert.NotNull(error);
    }

    #endregion
}
