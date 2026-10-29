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
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.DPoP;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client.ProofOfPossession;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.OpenId.Client.Tests.ProofOfPossession;

internal sealed class DPoPTestContext : IAsyncDisposable
{
    public const string KeyRingName = "dpop";

    private const string Authority = "https://op.example.test";
    private const string ClientId = "client-1";

    private ServiceProvider ServiceProvider { get; }
    private ECDsa Ec { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private RSA Rsa { get; } = RSA.Create(2048);

    public SecretKey EcKey { get; }
    public SecretKey RsaKey { get; }
    public DPoPNonceCache NonceCache { get; } = new();
    public IDPoPProofService ProofService { get; }

    public DPoPTestContext()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        var factory = ServiceProvider.GetRequiredService<ISecretKeyFactory>();
        EcKey = factory.CreateEcc(new KeyMetadata { KeyId = "ec-1" }, Ec);
        RsaKey = factory.CreateRsa(new KeyMetadata { KeyId = "rsa-1" }, Rsa);
        ProofService = ServiceProvider.GetRequiredService<IDPoPProofService>();
    }

    public DPoPKeyResolver CreateResolver(params SecretKey[] keys) =>
        new(
            new FakeKeyRingProvider(
                ServiceProvider.GetRequiredService<ISecretKeyCollectionFactory>().Create(keys)
            ),
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            ServiceProvider.GetRequiredService<ICredentialSelector>(),
            ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            ProofService,
            NonceCache
        );

    public DPoPProofKey CreateProofKey(SecretKey key) =>
        CreateResolver(key)
            .Resolve(
                new ClientRegistration(Authority, ClientId, "none") { DPoPKeyRing = KeyRingName },
                new OpenIdProviderMetadata { DPoPSigningAlgValuesSupported = ["ES256", "PS256"] },
                null
            )
        ?? throw new InvalidOperationException("The DPoP key was not resolved.");

    public string Thumbprint(SecretKey key) =>
        ServiceProvider
            .GetRequiredService<ISecretKeyTypeCatalog>()
            .TryGet(key.KeyType, out var descriptor)
        && descriptor.ExportPublicKey(key) is { } exported
            ? exported.Jwk.ComputeThumbprint()
            : throw new InvalidOperationException("The key cannot be exported.");

    public static JsonElement DecodeHeader(string jwt) => DecodeSegment(jwt, 0);

    public static JsonElement DecodePayload(string jwt) => DecodeSegment(jwt, 1);

    private static JsonElement DecodeSegment(string jwt, int index)
    {
        using var document = JsonDocument.Parse(
            System.Buffers.Text.Base64Url.DecodeFromChars(jwt.Split('.')[index])
        );
        return document.RootElement.Clone();
    }

    public async ValueTask DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
        Ec.Dispose();
        Rsa.Dispose();
    }

    private sealed class FakeKeyRingProvider(ISecretKeyCollection keys) : IClientKeyRingProvider
    {
        public ISecretKeyCollection GetKeyRing(string keyRing) =>
            keyRing == KeyRingName
                ? keys
                : throw new InvalidOperationException(
                    $"The key ring '{keyRing}' is not registered."
                );
    }
}
