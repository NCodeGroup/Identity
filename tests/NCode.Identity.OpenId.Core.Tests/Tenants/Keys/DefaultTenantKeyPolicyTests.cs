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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class DefaultTenantKeyPolicyTests : IAsyncLifetime
{
    private static TenantKeySpecification Rs256 { get; } =
        new()
        {
            Algorithm = "RS256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
        };

    private readonly ServiceProvider _serviceProvider;

    public DefaultTenantKeyPolicyTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                ICollectionDataSource<Algorithm>,
                UntypedKeyAlgorithmDataSource
            >()
        );
        _serviceProvider = serviceCollection.BuildServiceProvider();

        Policy = new DefaultTenantKeyPolicy(
            _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            _serviceProvider.GetRequiredService<ISecretKeyTypeCatalog>()
        );
    }

    private DefaultTenantKeyPolicy Policy { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _serviceProvider.DisposeAsync();

    // A registered signature algorithm keyed by the abstract SecretKey, which no secret key type describes.
    private sealed class UntypedKeySignatureAlgorithm : SignatureAlgorithm
    {
        public const string AlgorithmCode = "X-UNTYPED";

        public override string Code => AlgorithmCode;

        public override Type KeyType => typeof(SecretKey);

        public override IEnumerable<KeySizes> KeyBitSizes => [];

        public override HashAlgorithmName HashAlgorithmName => HashAlgorithmName.SHA256;

        public override int GetSignatureSizeBytes(int keySizeBits) => 0;

        public override bool TrySign(
            SecretKey secretKey,
            ReadOnlySpan<byte> inputData,
            Span<byte> signature,
            out int bytesWritten
        ) => throw new NotSupportedException();

        public override bool Verify(
            SecretKey secretKey,
            ReadOnlySpan<byte> inputData,
            ReadOnlySpan<byte> signature
        ) => throw new NotSupportedException();
    }

    private sealed class UntypedKeyAlgorithmDataSource : ICollectionDataSource<Algorithm>
    {
        public IEnumerable<Algorithm> Collection { get; } = [new UntypedKeySignatureAlgorithm()];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }

    #region ResolveSpecifications Tests

    [Fact]
    public void ResolveSpecifications_WhenNoKeysConfigured_ResolvesTheDefaultSet()
    {
        var resolution = Policy.ResolveSpecifications(new OpenIdTenantKeyProvisioningOptions());

        Assert.Empty(resolution.Errors);
        Assert.Collection(
            resolution.Specifications,
            specification =>
                Assert.Equal(
                    CreateSpecification(
                        AlgorithmCodes.DigitalSignature.RsaSha256,
                        SecretKeyUses.Signature,
                        SecretKeyTypes.Rsa,
                        3072
                    ),
                    specification
                ),
            specification =>
                Assert.Equal(
                    CreateSpecification(
                        AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
                        SecretKeyUses.Signature,
                        SecretKeyTypes.Rsa,
                        3072
                    ),
                    specification
                ),
            specification =>
                Assert.Equal(
                    CreateSpecification(
                        AlgorithmCodes.DigitalSignature.EcdsaSha256,
                        SecretKeyUses.Signature,
                        SecretKeyTypes.Ecc,
                        256
                    ),
                    specification
                )
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenDisabled_ResolvesNothing()
    {
        var resolution = Policy.ResolveSpecifications(
            new OpenIdTenantKeyProvisioningOptions { Enabled = false }
        );

        Assert.Empty(resolution.Specifications);
        Assert.Empty(resolution.Errors);
    }

    [Fact]
    public void ResolveSpecifications_WhenKeysConfigured_ReplacesTheDefaultSet()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "ES384" })
        );

        Assert.Empty(resolution.Errors);
        var specification = Assert.Single(resolution.Specifications);
        Assert.Equal(
            CreateSpecification("ES384", SecretKeyUses.Signature, SecretKeyTypes.Ecc, 384),
            specification
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenKeySizeUnset_UsesTheSmallestSizeTheAlgorithmAccepts()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "RS256" })
        );

        var specification = Assert.Single(resolution.Specifications);
        Assert.Equal(2048, specification.KeySizeBits);
    }

    [Fact]
    public void ResolveSpecifications_WhenKeyManagementAlgorithm_ResolvesAnEncryptionKey()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = AlgorithmCodes.KeyManagement.RsaOaep256,
                    KeySizeBits = 3072,
                }
            )
        );

        Assert.Empty(resolution.Errors);
        var specification = Assert.Single(resolution.Specifications);
        Assert.Equal(
            CreateSpecification(
                AlgorithmCodes.KeyManagement.RsaOaep256,
                SecretKeyUses.Encryption,
                SecretKeyTypes.Rsa,
                3072
            ),
            specification
        );
    }

    [Theory]
    [InlineData("XX999", "is not a registered signature or key-management algorithm")]
    [InlineData("HS256", "uses a symmetric key")]
    [InlineData(
        UntypedKeySignatureAlgorithm.AlgorithmCode,
        "uses a key type that has no registered secret key type"
    )]
    [InlineData("", "does not name an algorithm")]
    public void ResolveSpecifications_WhenAlgorithmCannotBeProvisioned_ReportsIt(
        string algorithm,
        string expectedReason
    )
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions { Algorithm = "ES256" },
                new OpenIdTenantKeyOptions { Algorithm = algorithm }
            )
        );

        var error = Assert.Single(resolution.Errors);
        Assert.Contains(expectedReason, error, StringComparison.Ordinal);
        var specification = Assert.Single(resolution.Specifications);
        Assert.Equal("ES256", specification.Algorithm);
    }

    [Fact]
    public void ResolveSpecifications_WhenAlgorithmListedTwice_ReportsIt()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions { Algorithm = "ES256" },
                new OpenIdTenantKeyOptions { Algorithm = "ES256" }
            )
        );

        var error = Assert.Single(resolution.Errors);
        Assert.Contains("'ES256' is listed more than once", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveSpecifications_WhenKeySizeNotAccepted_ReportsIt()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "RS256", KeySizeBits = 1024 })
        );

        Assert.Empty(resolution.Specifications);
        var error = Assert.Single(resolution.Errors);
        Assert.Contains("'1024' bits is not valid", error, StringComparison.Ordinal);
    }

    #endregion

    #region IsSatisfiedBy Tests

    [Fact]
    public void IsSatisfiedBy_WhenSecretIsBoundToTheAlgorithm_ReturnsTrue()
    {
        var secret = CreateSecret(algorithm: "RS256", use: SecretKeyUses.Signature);

        Assert.True(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Theory]
    [InlineData("RS256")]
    [InlineData("PS256")]
    public void IsSatisfiedBy_WhenSecretIsUnbound_ReturnsTrueForEveryAlgorithmItFits(
        string algorithm
    )
    {
        var secret = CreateSecret(algorithm: null, use: null);

        Assert.True(
            Policy.IsSatisfiedBy(
                CreateSpecification(algorithm, SecretKeyUses.Signature, SecretKeyTypes.Rsa, 3072),
                secret
            )
        );
    }

    [Fact]
    public void IsSatisfiedBy_WhenSecretIsBoundToAnotherAlgorithm_ReturnsFalse()
    {
        var secret = CreateSecret(algorithm: "PS256", use: SecretKeyUses.Signature);

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Theory]
    [InlineData(SecretEncodings.Spki)]
    [InlineData(SecretEncodings.X509)]
    public void IsSatisfiedBy_WhenSecretHoldsOnlyPublicMaterial_ReturnsFalse(string encoding)
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            encoding: encoding
        );

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Fact]
    public void IsSatisfiedBy_WhenSecretIsHeldInAnUnknownEncoding_TrustsItRatherThanDuplicatingIt()
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            encoding: "keystore_ref"
        );

        Assert.True(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Fact]
    public void IsSatisfiedBy_WhenSecretIsForAnotherUse_ReturnsFalse()
    {
        var secret = CreateSecret(algorithm: null, use: SecretKeyUses.Encryption);

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Theory]
    [InlineData(SecretKeyTypes.Ecc, 256)]
    [InlineData(SecretKeyTypes.Rsa, 1024)]
    [InlineData("unregistered", 3072)]
    public void IsSatisfiedBy_WhenSecretTypeOrSizeDoesNotFitTheAlgorithm_ReturnsFalse(
        string secretType,
        int keySizeBits
    )
    {
        var secret = CreateSecret(
            algorithm: null,
            use: SecretKeyUses.Signature,
            secretType: secretType,
            keySizeBits: keySizeBits
        );

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    #endregion

    #region Helpers

    private static OpenIdTenantKeyProvisioningOptions CreateOptions(
        params OpenIdTenantKeyOptions[] keys
    ) => new() { Keys = [.. keys] };

    private static TenantKeySpecification CreateSpecification(
        string algorithm,
        string use,
        string secretType,
        int keySizeBits
    ) =>
        new()
        {
            Algorithm = algorithm,
            Use = use,
            SecretType = secretType,
            KeySizeBits = keySizeBits,
        };

    private static PersistedSecret CreateSecret(
        string? algorithm,
        string? use,
        string encoding = SecretEncodings.Pkcs8,
        string secretType = SecretKeyTypes.Rsa,
        int keySizeBits = 3072
    ) =>
        new()
        {
            SecretId = "secret",
            Use = use,
            Algorithm = algorithm,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = secretType,
            SecretEncoding = encoding,
            KeySizeBits = keySizeBits,
            EncodedValue = "AA",
        };

    #endregion
}
