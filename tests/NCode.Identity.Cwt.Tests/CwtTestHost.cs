// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Cose;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cwt.Issuance;
using NCode.Identity.Jose;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cwt.Tests;

/// <summary>
/// The real composition the CWT tests run against: the Secrets, JOSE, COSE and CWT libraries over a fake clock.
/// </summary>
public sealed class CwtTestHost : IAsyncDisposable
{
    public const string ClaimsA1 =
        "A70175636F61703A2F2F61732E6578616D706C652E636F6D02656572696B77037818636F61703A2F2F6C696768742E6578616D706C652E636F6D041A5612AEB0051A5610D9F0061A5610D9F007420B71";

    public const string EcX = "143329CCE7868E416927599CF65A34F3CE2FFDA55A7ECA69ED8919A394D42F0F";
    public const string EcY = "60F7F1A780D8A783BFB7A2DD6B2796E8128DBBCEF9D3D168DB9529971A36E7B9";
    public const string EcD = "6C1382765AEC5358F117733D281C1C7BDC39884D04A45A1E6C67C858BC206C19";
    public const string HmacKey =
        "403697DE87AF64611C1D32A05DAB0FE1FCB715A86AB435F1EC99192D79569388";
    public const string AesKey = "849B57219DAE48DE646D07DBB533566E";

    public static DateTimeOffset StartTime { get; } = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public CwtTestHost(
        DateTimeOffset? now = null,
        IEnumerable<CoseAlgorithm>? extraAlgorithms = null
    )
    {
        TimeProvider = new FakeTimeProvider(now ?? StartTime);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider);
        var cose = services.AddSecretsLibrary().AddJoseLibrary().AddCoseLibrary();
        foreach (var algorithm in extraAlgorithms ?? [])
            cose.AddCoseAlgorithm(algorithm);

        cose.AddCwtLibrary();
        Services = services.BuildServiceProvider();
    }

    public FakeTimeProvider TimeProvider { get; }

    public ServiceProvider Services { get; }

    public ICwtIssuer Issuer => Services.GetRequiredService<ICwtIssuer>();

    public ICoseSerializer Serializer => Services.GetRequiredService<ICoseSerializer>();

    public ISecretKeyFactory KeyFactory => Services.GetRequiredService<ISecretKeyFactory>();

    public static byte[] Hex(string hex) => Convert.FromHexString(hex);

    public SecretKey EcPrivateKey(string? keyId = null) => EcKey(EcD, keyId);

    public SecretKey EcPublicKey(string? keyId = null) => EcKey(null, keyId);

    private SecretKey EcKey(string? d, string? keyId)
    {
        var key = ECDsa.Create(
            new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = Hex(EcX), Y = Hex(EcY) },
                D = d is null ? null : Hex(d),
            }
        );
        var metadata = new KeyMetadata { KeyId = keyId };
        return d is null
            ? KeyFactory.CreateEccPublicKey(metadata, key)
            : KeyFactory.CreateEcc(metadata, key);
    }

    public SecretKey Hmac(string? keyId = null) =>
        KeyFactory.CreateSymmetric(new KeyMetadata { KeyId = keyId }, Hex(HmacKey));

    public SecretKey Aes(string? keyId = null) =>
        KeyFactory.CreateSymmetric(new KeyMetadata { KeyId = keyId }, Hex(AesKey));

    public ValueTask DisposeAsync() => Services.DisposeAsync();
}
