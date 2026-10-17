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
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

public sealed class CapabilitiesProvidersTests
{
    [Fact]
    public async Task SecretTypeCapabilitiesProvider_AddsSecretTypesSection()
    {
        var mockDescriptor = new Mock<ISecretKeyTypeDescriptor>(MockBehavior.Strict);
        mockDescriptor.Setup(x => x.KeyType).Returns("rsa");
        mockDescriptor.Setup(x => x.IsAsymmetric).Returns(true);
        mockDescriptor.Setup(x => x.SupportsPublicKeyExport).Returns(true);
        mockDescriptor.Setup(x => x.SupportsPublicKeyImport).Returns(true);

        var mockCatalog = new Mock<ISecretKeyTypeCatalog>(MockBehavior.Strict);
        mockCatalog.Setup(x => x.Descriptors).Returns([mockDescriptor.Object]);

        var provider = new SecretTypeCapabilitiesProvider(mockCatalog.Object);
        var capabilities = new Dictionary<string, object>();

        await provider.HandleAsync(
            new DiscoverManagementCapabilitiesCommand(null!, capabilities),
            CancellationToken.None
        );

        var section = Assert.IsType<List<SecretTypeCapabilityResource>>(
            capabilities[SecretTypeCapabilitiesProvider.SectionName]
        );
        var item = Assert.Single(section);
        Assert.Equal("rsa", item.SecretType);
        Assert.True(item.IsAsymmetric);
        Assert.True(item.SupportsPublicKeyExport);
        Assert.True(item.SupportsPublicKeyImport);
    }

    [Fact]
    public async Task EccCurveCapabilitiesProvider_AddsEccCurvesSection()
    {
        var mockCatalog = new Mock<IEccCurveCatalog>(MockBehavior.Strict);
        mockCatalog
            .Setup(x => x.Curves)
            .Returns([
                new EccCurveSpecification
                {
                    CurveName = "P-256",
                    CurveSizeBits = 256,
                    Curve = ECCurve.NamedCurves.nistP256,
                },
            ]);

        var provider = new EccCurveCapabilitiesProvider(mockCatalog.Object);
        var capabilities = new Dictionary<string, object>();

        await provider.HandleAsync(
            new DiscoverManagementCapabilitiesCommand(null!, capabilities),
            CancellationToken.None
        );

        var section = Assert.IsType<List<EccCurveCapabilityResource>>(
            capabilities[EccCurveCapabilitiesProvider.SectionName]
        );
        var item = Assert.Single(section);
        Assert.Equal("P-256", item.CurveName);
        Assert.Equal(256, item.CurveSizeBits);
    }

    [Fact]
    public async Task AlgorithmCapabilitiesProvider_PublishesSignatureAndKeyManagementAlgorithms()
    {
        // Resolve the real algorithm collection and key-type catalog so the section is asserted against the exact
        // set of algorithms and key types the server supports — the same source of truth the provider reads.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var provider = new AlgorithmCapabilitiesProvider(
            serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            serviceProvider.GetRequiredService<ISecretKeyTypeCatalog>()
        );
        var capabilities = new Dictionary<string, object>();

        await provider.HandleAsync(
            new DiscoverManagementCapabilitiesCommand(null!, capabilities),
            CancellationToken.None
        );

        var section = Assert.IsType<List<AlgorithmCapabilityResource>>(
            capabilities[AlgorithmCapabilitiesProvider.SectionName]
        );

        // Only signature and key-management algorithms are published (no AEAD or compression algorithms).
        Assert.NotEmpty(section);
        Assert.All(
            section,
            algorithm =>
                Assert.Contains(
                    algorithm.Use,
                    new[] { SecretKeyUses.Signature, SecretKeyUses.Encryption }
                )
        );

        var rs256 = Assert.Single(
            section,
            algorithm => algorithm.Code == AlgorithmCodes.DigitalSignature.RsaSha256
        );
        Assert.Equal(SecretKeyUses.Signature, rs256.Use);
        Assert.Contains(SecretKeyTypes.Rsa, rs256.SecretTypes);
        Assert.NotEmpty(rs256.KeyBitSizes);

        var rsaOaep = Assert.Single(
            section,
            algorithm => algorithm.Code == AlgorithmCodes.KeyManagement.RsaOaep
        );
        Assert.Equal(SecretKeyUses.Encryption, rsaOaep.Use);
        Assert.Contains(SecretKeyTypes.Rsa, rsaOaep.SecretTypes);
    }
}
