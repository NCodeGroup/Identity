// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.Jose.Secp256k1.Tests;

/// <summary>
/// A service provider composed with the Secrets and JOSE libraries and secp256k1 support, and helpers that create
/// <c>ecc</c> keys through the registered key-type descriptor.
/// </summary>
public sealed class Secp256k1TestServices : IAsyncDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Secp256k1TestServices"/> class.
    /// </summary>
    public Secp256k1TestServices()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddSecp256k1();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Assert.True(
            ServiceProvider
                .GetRequiredService<ISecretKeyTypeCatalog>()
                .TryGet(SecretKeyTypes.Ecc, out var descriptor)
        );
        Descriptor = descriptor;
        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        JoseSerializer = ServiceProvider.GetRequiredService<IJoseSerializer>();
    }

    /// <summary>
    /// Gets the composed service provider.
    /// </summary>
    public ServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the <c>ecc</c> key-type descriptor.
    /// </summary>
    public ISecretKeyTypeDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the registered algorithms.
    /// </summary>
    public IAlgorithmCollection AlgorithmCollection { get; }

    /// <summary>
    /// Gets the <c>JOSE</c> serializer.
    /// </summary>
    public IJoseSerializer JoseSerializer { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await ServiceProvider.DisposeAsync();

    /// <summary>
    /// Gets a registered signature algorithm.
    /// </summary>
    /// <param name="code">The algorithm's code.</param>
    /// <returns>The algorithm.</returns>
    public SignatureAlgorithm GetSignatureAlgorithm(string code)
    {
        Assert.True(AlgorithmCollection.TryGetSignatureAlgorithm(code, out var algorithm));
        return algorithm;
    }

    /// <summary>
    /// Generates a private <c>ecc</c> key on the named curve, which P-256 and secp256k1 share a size with.
    /// </summary>
    /// <param name="curve">The <c>JWK</c> <c>crv</c> name.</param>
    /// <returns>The private key.</returns>
    public EccSecretKey GenerateKey(string curve)
    {
        byte[] pkcs8 = [];
        var generated = Descriptor.GenerateKeyMaterial(
            new SecretKeyGenerationParameters { KeySizeBits = 256, Curve = curve },
            bytes =>
            {
                pkcs8 = bytes.ToArray();
                return string.Empty;
            }
        );
        Assert.Equal(SecretEncodings.Pkcs8, generated.SecretEncoding);
        return Deserialize(SecretEncodings.Pkcs8, pkcs8);
    }

    /// <summary>
    /// Creates a private <c>ecc</c> key from its parameters.
    /// </summary>
    /// <param name="parameters">The key's curve, public point and private scalar.</param>
    /// <returns>The private key.</returns>
    public EccSecretKey CreatePrivateKey(ECParameters parameters)
    {
        using var ecdsa = ECDsa.Create(parameters);
        return Deserialize(SecretEncodings.Pkcs8, ecdsa.ExportPkcs8PrivateKey());
    }

    /// <summary>
    /// Imports a public <c>ecc</c> key from a <c>JWK</c>.
    /// </summary>
    /// <param name="jwk">The <c>JWK</c>.</param>
    /// <returns>The public key.</returns>
    public EccSecretKey ImportPublicJwk(JsonObject jwk)
    {
        byte[] spki = [];
        var imported = Descriptor.ImportPublicKeyMaterial(
            new SecretKeyImportParameters
            {
                Format = SecretKeyImportFormats.Jwk,
                KeyMaterial = jwk.ToJsonString(),
            },
            bytes =>
            {
                spki = bytes.ToArray();
                return string.Empty;
            }
        );
        Assert.Equal(SecretEncodings.Spki, imported.SecretEncoding);
        return Deserialize(SecretEncodings.Spki, spki);
    }

    private EccSecretKey Deserialize(string encoding, byte[] keyMaterial) =>
        Assert.IsType<EccSecretKey>(
            Descriptor.DeserializeKeyMaterial(
                new KeyMetadata { KeyId = "secp256k1-test", Use = SecretKeyUses.Signature },
                encoding,
                keyMaterial
            ),
            exactMatch: false
        );
}
