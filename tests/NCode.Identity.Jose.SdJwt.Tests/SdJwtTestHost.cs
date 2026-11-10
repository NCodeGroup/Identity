// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.SdJwt.Issuance;
using NCode.Identity.Jose.SdJwt.Options;
using NCode.Identity.Jose.SdJwt.Presentation;
using NCode.Identity.Jose.SdJwt.Verification;
using NCode.Identity.Jwk;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.SdJwt.Tests;

/// <summary>
/// The real composition the SD-JWT tests run against: the Secrets, JOSE, JWT and SD-JWT libraries over a fake clock,
/// with an issuer key, a holder key and a stranger key on P-256.
/// </summary>
public sealed class SdJwtTestHost : IAsyncDisposable
{
    public const string IssuerKeyId = "issuer";
    public const string HolderKeyId = "holder";
    public const string StrangerKeyId = "stranger";
    public const string Es256 = AlgorithmCodes.DigitalSignature.EcdsaSha256;

    public static DateTimeOffset StartTime { get; } = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public SdJwtTestHost(Action<SdJwtOptions>? configureOptions = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<TimeProvider>(TimeProvider);
        var jwt = serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        if (configureOptions is null)
            jwt.AddSdJwt();
        else
            jwt.AddSdJwt(configureOptions);
        ServiceProvider = serviceCollection.BuildServiceProvider();

        var secretKeyFactory = ServiceProvider.GetRequiredService<ISecretKeyFactory>();
        IssuerKey = CreateEccKey(secretKeyFactory, IssuerKeyId);
        HolderKey = CreateEccKey(secretKeyFactory, HolderKeyId);
        StrangerKey = CreateEccKey(secretKeyFactory, StrangerKeyId);
    }

    public FakeTimeProvider TimeProvider { get; } = new(StartTime);

    public ServiceProvider ServiceProvider { get; }

    public SecretKey IssuerKey { get; }

    public SecretKey HolderKey { get; }

    public SecretKey StrangerKey { get; }

    public IAlgorithmCollection Algorithms =>
        ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>().Collection;

    public IJoseSerializer JoseSerializer => ServiceProvider.GetRequiredService<IJoseSerializer>();

    public ISdJwtIssuer Issuer => ServiceProvider.GetRequiredService<ISdJwtIssuer>();

    public ISdJwtHolder Holder => ServiceProvider.GetRequiredService<ISdJwtHolder>();

    public ISdJwtVerifier Verifier => ServiceProvider.GetRequiredService<ISdJwtVerifier>();

    public JoseSigningCredentials IssuerCredentials => CreateCredentials(IssuerKey);

    public JoseSigningCredentials HolderCredentials => CreateCredentials(HolderKey);

    public JoseSigningCredentials StrangerCredentials => CreateCredentials(StrangerKey);

    public JsonWebKey HolderJwk => ExportPublicJwk(HolderKey);

    public JoseSigningCredentials CreateCredentials(
        SecretKey secretKey,
        string algorithmCode = Es256
    ) =>
        Algorithms.TryGetSignatureAlgorithm(algorithmCode, out var algorithm)
            ? new JoseSigningCredentials(secretKey, algorithm)
            : throw new InvalidOperationException(algorithmCode);

    public JsonWebKey ExportPublicJwk(SecretKey secretKey)
    {
        var catalog = ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>();
        Assert.True(catalog.TryGet(secretKey.KeyType, out var descriptor));
        var exported = descriptor.ExportPublicKey(secretKey);
        Assert.NotNull(exported);
        return exported.Jwk;
    }

    /// <summary>
    /// The whole policy of the issuer-signed JWT: the issuer's key and <c>ES256</c>.
    /// </summary>
    public JwtValidationParameters CreateIssuerJwtParameters(SecretKey? issuerKey = null) =>
        new() { ValidationKeys = [issuerKey ?? IssuerKey], ValidSignatureAlgorithms = [Es256] };

    /// <summary>
    /// Signs a hand-built issuer-signed JWT payload with the issuer's key, for presentations the issuer never makes.
    /// </summary>
    public ValueTask<string> SignIssuerJwtAsync(
        string payloadJson,
        string tokenType,
        CancellationToken cancellationToken
    ) => SignJwtAsync(IssuerCredentials, payloadJson, tokenType, cancellationToken);

    public ValueTask<string> SignJwtAsync(
        JoseSigningCredentials credentials,
        string payloadJson,
        string tokenType,
        CancellationToken cancellationToken
    )
    {
        var signature = new JoseSignatureOptions(credentials) { TokenType = tokenType };
        var encoder = JoseSerializer.CreateEncoder(new JoseSigningOptions(signature));
        return encoder.EncodeAsync(payloadJson, cancellationToken);
    }

    /// <summary>
    /// Encloses a compact JWS in a JWE to the specified elliptic-curve key, with <c>cty</c> <c>JWT</c>, as a Nested
    /// JWT (RFC 7519 §5.2).
    /// </summary>
    public ValueTask<string> EncloseInJweAsync(
        string jws,
        SecretKey recipientKey,
        CancellationToken cancellationToken
    )
    {
        if (
            !Algorithms.TryGetKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.EcdhEsAes128,
                out var keyManagement
            )
        )
            throw new InvalidOperationException(AlgorithmCodes.KeyManagement.EcdhEsAes128);

        if (
            !Algorithms.TryGetAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,
                out var contentEncryption
            )
        )
            throw new InvalidOperationException(
                AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256
            );

        var credentials = new JoseEncryptionCredentials(
            recipientKey,
            keyManagement,
            contentEncryption
        );
        var options = new JoseEncryptionOptions(credentials)
        {
            ProtectedHeaders = new Dictionary<string, object>
            {
                [JoseClaimNames.Header.Cty] = JoseTokenTypes.Jwt,
            },
        };
        return JoseSerializer.CreateEncoder(options).EncodeAsync(jws, cancellationToken);
    }

    /// <summary>
    /// Decodes a JWT with the specified key, which verifies its signature.
    /// </summary>
    public ValueTask<JsonElement> DecodePayloadAsync(
        string jwt,
        SecretKey secretKey,
        CancellationToken cancellationToken
    ) => JoseSerializer.DeserializeAsync<JsonElement>(jwt, secretKey, cancellationToken);

    public async ValueTask DisposeAsync() => await ServiceProvider.DisposeAsync();

    private static SecretKey CreateEccKey(ISecretKeyFactory secretKeyFactory, string keyId) =>
        secretKeyFactory.CreateEcc(
            new KeyMetadata { KeyId = keyId },
            ECDsa.Create(ECCurve.NamedCurves.nistP256)
        );
}
