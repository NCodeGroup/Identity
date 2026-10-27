#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.DPoP;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests;

/// <summary>
/// Composes the JOSE DPoP proof service with the opt-in <c>EdDSA</c> package to show that an <c>OKP</c> proof key
/// needs no DPoP-specific code.
/// </summary>
public sealed class EdDsaDPoPProofTests : IAsyncDisposable
{
    private ServiceProvider ServiceProvider { get; }

    public EdDsaDPoPProofTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        serviceCollection.AddEdDsa();
        ServiceProvider = serviceCollection.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
    }

    [Theory]
    [InlineData("EdDSA", OkpCurves.Ed25519Name, 256)]
    [InlineData("EdDSA", OkpCurves.Ed448Name, 456)]
    [InlineData("Ed25519", OkpCurves.Ed25519Name, 256)]
    [InlineData("Ed448", OkpCurves.Ed448Name, 456)]
    public void CreateProof_WhenOkpKey_ValidatesWithThumbprint(
        string alg,
        string curve,
        int keySizeBits
    )
    {
        var catalog = ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>();
        Assert.True(catalog.TryGetByJwkKeyType("OKP", out var descriptor));
        byte[] keyMaterial = [];
        var generated = descriptor.GenerateKeyMaterial(
            new SecretKeyGenerationParameters { KeySizeBits = keySizeBits, Curve = curve },
            bytes =>
            {
                keyMaterial = bytes.ToArray();
                return string.Empty;
            }
        );
        var secretKey = descriptor.DeserializeKeyMaterial(
            new KeyMetadata { KeyId = "dpop" },
            generated.SecretEncoding,
            keyMaterial
        );
        var algorithms = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        Assert.True(algorithms.TryGetSignatureAlgorithm(alg, out var algorithm));
        var proofService = ServiceProvider.GetRequiredService<IDPoPProofService>();

        var proof = proofService.CreateProof(
            new DPoPProofCreationOptions
            {
                SigningCredentials = new JoseSigningCredentials(secretKey, algorithm),
                HttpMethod = "POST",
                HttpUri = "https://op.test/token",
            }
        );

        var valid = proofService.TryValidateProof(
            proof,
            new DPoPProofValidationParameters
            {
                HttpMethod = "POST",
                HttpUri = "https://op.test/token",
                AllowedAlgorithms = [alg],
                Lifetime = TimeSpan.FromSeconds(60),
            },
            out var result,
            out var error
        );

        Assert.True(valid, error);
        Assert.NotNull(result);
        Assert.Equal(alg, result.Algorithm);
        Assert.Equal("OKP", result.PublicKey.KeyType);
        Assert.Equal(result.PublicKey.ComputeThumbprint(), result.JwkThumbprint);
    }
}
