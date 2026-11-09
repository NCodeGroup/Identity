// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.AuthenticatedEncryption;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Tests.Algorithms.Signature;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Credentials;

public class DefaultCredentialSelectorTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static EccSecretKey CreateKey(ECCurve curve, string keyId) =>
        CreateKey(curve, new KeyMetadata { KeyId = keyId });

    private static EccSecretKey CreateKey(ECCurve curve, KeyMetadata metadata)
    {
        using var key = ECDsa.Create(curve);
        return SecretKeyFactory.CreateEcc(metadata, key);
    }

    private static DefaultAlgorithmCollection CreateEs256Collection() =>
        new([
            EccSignatureAlgorithmFactory.Create(
                AlgorithmCodes.DigitalSignature.EcdsaSha256,
                ECCurve.NamedCurves.nistP256,
                HashAlgorithmName.SHA256
            ),
        ]);

    private static RsaSecretKey CreateRsaKey(KeyMetadata metadata)
    {
        using var key = RSA.Create(2048);
        return SecretKeyFactory.CreateRsa(metadata, key);
    }

    private static DefaultAlgorithmCollection CreateRsaOaepCollection() =>
        new([
            new RsaOaepKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep,
                HashAlgorithmName.SHA1
            ),
            new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm,
                128
            ),
        ]);

    private static DefaultCredentialSelector CreateSelector() => new(new FakeTimeProvider(Now));

    private static bool TryGetRsaOaepCredentials(
        IReadOnlyCollection<SecretKey> candidateKeys,
        out JoseEncryptionCredentials? credentials
    ) =>
        CreateSelector()
            .TryGetEncryptionCredentials(
                CreateRsaOaepCollection(),
                [AlgorithmCodes.KeyManagement.RsaOaep],
                [AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm],
                [],
                candidateKeys,
                out credentials
            );

    #region TryGetSigningCredentials Tests

    [Fact]
    public void TryGetSigningCredentials_WhenFirstKeyHasWrongCurve_SelectsMatchingCurveKey()
    {
        var collection = new DefaultAlgorithmCollection([
            EccSignatureAlgorithmFactory.Create(
                AlgorithmCodes.DigitalSignature.EcdsaSha256,
                ECCurve.NamedCurves.nistP256,
                HashAlgorithmName.SHA256
            ),
        ]);
        var wrongCurveKey = CreateKey(ECCurve.NamedCurves.brainpoolP256r1, "brainpool");
        var p256Key = CreateKey(ECCurve.NamedCurves.nistP256, "p256");

        var selector = CreateSelector();
        var result = selector.TryGetSigningCredentials(
            collection,
            ["ES256"],
            [wrongCurveKey, p256Key],
            out var credentials
        );

        Assert.True(result);
        Assert.NotNull(credentials);
        Assert.Same(p256Key, credentials.SecretKey);
    }

    [Fact]
    public void TryGetSigningCredentials_WhenTheFirstKeyIsNotActiveYet_SelectsTheActiveKey()
    {
        // The upcoming key is published ahead of its activation and fits the algorithm, yet it signs nothing until
        // it activates (ADR-1403).
        var upcomingKey = CreateKey(
            ECCurve.NamedCurves.nistP256,
            new KeyMetadata { KeyId = "upcoming", ActivatesWhen = Now.AddSeconds(1) }
        );
        var activeKey = CreateKey(
            ECCurve.NamedCurves.nistP256,
            new KeyMetadata { KeyId = "active", ActivatesWhen = Now }
        );

        var result = CreateSelector()
            .TryGetSigningCredentials(
                CreateEs256Collection(),
                ["ES256"],
                [upcomingKey, activeKey],
                out var credentials
            );

        Assert.True(result);
        Assert.NotNull(credentials);
        Assert.Same(activeKey, credentials.SecretKey);
    }

    [Fact]
    public void TryGetSigningCredentials_WhenTheFirstKeyIsRetired_SelectsTheActiveKey()
    {
        var retiredKey = CreateKey(
            ECCurve.NamedCurves.nistP256,
            new KeyMetadata
            {
                KeyId = "retired",
                RetiresWhen = Now,
                ExpiresWhen = Now.AddDays(7),
            }
        );
        var activeKey = CreateKey(
            ECCurve.NamedCurves.nistP256,
            new KeyMetadata { KeyId = "active", RetiresWhen = Now.AddSeconds(1) }
        );

        var result = CreateSelector()
            .TryGetSigningCredentials(
                CreateEs256Collection(),
                ["ES256"],
                [retiredKey, activeKey],
                out var credentials
            );

        Assert.True(result);
        Assert.NotNull(credentials);
        Assert.Same(activeKey, credentials.SecretKey);
    }

    [Fact]
    public void TryGetSigningCredentials_WhenEveryKeyIsExpired_SelectsNone()
    {
        var expiredKey = CreateKey(
            ECCurve.NamedCurves.nistP256,
            new KeyMetadata { KeyId = "expired", ExpiresWhen = Now }
        );

        var result = CreateSelector()
            .TryGetSigningCredentials(
                CreateEs256Collection(),
                ["ES256"],
                [expiredKey],
                out var credentials
            );

        Assert.False(result);
        Assert.Null(credentials);
    }

    #endregion

    #region TryGetEncryptionCredentials Tests

    [Fact]
    public void TryGetEncryptionCredentials_WhenTheFirstKeyIsNotActiveYet_SelectsTheActiveKey()
    {
        // The active window gates encryption as it gates signing: a client encrypts to the key the server prefers,
        // so a key published ahead of its activation is not preferred until it activates.
        var upcomingKey = CreateRsaKey(
            new KeyMetadata { KeyId = "upcoming", ActivatesWhen = Now.AddSeconds(1) }
        );
        var activeKey = CreateRsaKey(new KeyMetadata { KeyId = "active", ActivatesWhen = Now });

        var result = TryGetRsaOaepCredentials([upcomingKey, activeKey], out var credentials);

        Assert.True(result);
        Assert.NotNull(credentials);
        Assert.Same(activeKey, credentials.SecretKey);
    }

    [Fact]
    public void TryGetEncryptionCredentials_WhenTheFirstKeyIsRetired_SelectsTheActiveKey()
    {
        var retiredKey = CreateRsaKey(
            new KeyMetadata
            {
                KeyId = "retired",
                RetiresWhen = Now,
                ExpiresWhen = Now.AddDays(7),
            }
        );
        var activeKey = CreateRsaKey(
            new KeyMetadata { KeyId = "active", RetiresWhen = Now.AddSeconds(1) }
        );

        var result = TryGetRsaOaepCredentials([retiredKey, activeKey], out var credentials);

        Assert.True(result);
        Assert.NotNull(credentials);
        Assert.Same(activeKey, credentials.SecretKey);
    }

    [Fact]
    public void TryGetEncryptionCredentials_WhenEveryKeyIsExpired_SelectsNone()
    {
        var expiredKey = CreateRsaKey(new KeyMetadata { KeyId = "expired", ExpiresWhen = Now });

        var result = TryGetRsaOaepCredentials([expiredKey], out var credentials);

        Assert.False(result);
        Assert.Null(credentials);
    }

    #endregion
}
