// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using NCode.Encoders;
using NCode.Identity.Jose;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jwt.Validation;

public sealed class DefaultValidationKeyResolverTests : IAsyncLifetime, IDisposable
{
    private ServiceProvider ServiceProvider { get; }
    private X509Certificate2 Certificate { get; }

    public DefaultValidationKeyResolverTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=resolver",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        Certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(10)
        );

        CertificateKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .Create(new KeyMetadata { KeyId = "certificate-key" }, Certificate);
        OtherKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateSymmetric(new KeyMetadata { KeyId = "other-key" }, new byte[32]);
        SecretKeys = ServiceProvider
            .GetRequiredService<ISecretKeyCollectionFactory>()
            .Create([OtherKey, CertificateKey]);
    }

    private SecretKey CertificateKey { get; }

    private SecretKey OtherKey { get; }

    private ISecretKeyCollection SecretKeys { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await ServiceProvider.DisposeAsync();

    public void Dispose() => Certificate.Dispose();

    private IEnumerable<SecretKey> Resolve(string name, string thumbprint) =>
        DefaultValidationKeyResolver.ResolveValidationKeys(
            JoseProtectionTypes.Jws,
            new JsonObject { ["alg"] = "RS256", [name] = thumbprint }.Deserialize<JsonElement>(),
            SecretKeys
        );

    [Theory]
    [InlineData(JoseClaimNames.Header.X5t, "SHA1")]
    [InlineData(JoseClaimNames.Header.X5tS256, "SHA256")]
    public void ResolveValidationKeys_WhenThumbprintMatchesACertificate_ResolvesItsKey(
        string name,
        string hashAlgorithm
    )
    {
        var thumbprint = Base64Url.Encode(
            Certificate.GetCertHash(new HashAlgorithmName(hashAlgorithm))
        );

        Assert.Same(CertificateKey, Assert.Single(Resolve(name, thumbprint)));
    }

    // A thumbprint is read from the token's header: one that is not base64url, or that decodes to a digest of another
    // size, refuses the token.
    [Theory]
    [InlineData(JoseClaimNames.Header.X5t, "!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    [InlineData(JoseClaimNames.Header.X5t, "AAAA")]
    [InlineData(JoseClaimNames.Header.X5t, "AAAAA")]
    [InlineData(JoseClaimNames.Header.X5t, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(JoseClaimNames.Header.X5tS256, "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    [InlineData(JoseClaimNames.Header.X5tS256, "AAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void ResolveValidationKeys_WhenThumbprintIsNotADigest_Throws(
        string name,
        string thumbprint
    )
    {
        var exception = Assert.Throws<JwtValidationException>(() => Resolve(name, thumbprint));

        Assert.Equal(
            $"The '{name}' header is not a base64url-encoded certificate thumbprint.",
            exception.Message
        );
    }
}
