// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Tests.ContributedKeyTypes;

/// <summary>
/// A key type the tests contribute: a made-up <c>XKP</c> JWK key type whose public member <c>pub</c> and private member
/// <c>sk</c> no standard key type uses, so anything a test reads or writes about its JSON Web Key form can come only from
/// that type's own rules. Its key material is a P-256 key, carried as a <c>SubjectPublicKeyInfo</c> and a PKCS#8
/// private key.
/// </summary>
internal static class XkpKeyType
{
    public const string SecretKeyTypeName = "xkp";
    public const string PublicMember = "pub";
    public const string PrivateMember = "sk";

    public static JsonWebKeyTypeDescriptor JwkKeyType { get; } =
        new("XKP", [PublicMember], [PrivateMember]);
}

internal sealed class XkpSecretKey(
    KeyMetadata metadata,
    int keySizeBits,
    byte[] subjectPublicKeyInfo,
    byte[]? pkcs8PrivateKey
) : AsymmetricSecretKey(metadata, keySizeBits, certificateRawData: null)
{
    public byte[] SubjectPublicKeyInfo { get; } = subjectPublicKeyInfo;

    public byte[]? Pkcs8PrivateKey { get; } = pkcs8PrivateKey;

    public override string KeyType => XkpKeyType.SecretKeyTypeName;

    public override void ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)
    {
        var span = destination.GetSpan(SubjectPublicKeyInfo.Length);
        SubjectPublicKeyInfo.CopyTo(span);
        destination.Advance(SubjectPublicKeyInfo.Length);
    }
}

internal sealed class XkpSecretKeyTypeDescriptor(ISecretKeyFactory secretKeyFactory)
    : AsymmetricSecretKeyTypeDescriptor(secretKeyFactory)
{
    public override string KeyType => XkpKeyType.SecretKeyTypeName;

    public override Type KeyClrType => typeof(XkpSecretKey);

    public override JsonWebKeyTypeDescriptor JwkKeyType => XkpKeyType.JwkKeyType;

    public override IReadOnlyCollection<string> PublicKeyImportFormats { get; } =
    [SecretKeyImportFormats.Jwk];

    public override string? ValidateGeneration(SecretKeyGenerationParameters parameters) =>
        RejectCurveIfPresent(parameters);

    public override GeneratedSecretKeyMaterial GenerateKeyMaterial(
        SecretKeyGenerationParameters parameters,
        SecretKeyMaterialProtector protect
    )
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return ExportProtectedPkcs8(ecdsa, protect);
    }

    protected override (byte[] SubjectPublicKeyInfo, int KeySizeBits) ParseJwkPublicKey(
        JsonElement root
    ) => ReadSubjectPublicKeyInfo(GetRequiredBase64UrlMember(root, XkpKeyType.PublicMember));

    protected override (byte[] SubjectPublicKeyInfo, int KeySizeBits) ImportPublicKeyPem(
        string pem
    ) => throw new NotSupportedException();

    protected override bool TryGetCertificateKeySizeBits(
        X509Certificate2 certificate,
        out int keySizeBits
    ) => throw new NotSupportedException();

    protected override SecretKey CreatePrivateKeyFromPkcs8(
        KeyMetadata metadata,
        ReadOnlySpan<byte> pkcs8
    )
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(pkcs8, out _);
        return new XkpSecretKey(
            metadata,
            ecdsa.KeySize,
            ecdsa.ExportSubjectPublicKeyInfo(),
            pkcs8.ToArray()
        );
    }

    protected override SecretKey CreatePublicKeyFromSpki(
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo
    )
    {
        var (spki, keySizeBits) = ReadSubjectPublicKeyInfo(subjectPublicKeyInfo.ToArray());
        return new XkpSecretKey(metadata, keySizeBits, spki, pkcs8PrivateKey: null);
    }

    protected override JsonObject? WritePublicKeyJwk(AsymmetricSecretKey secretKey) =>
        new()
        {
            [JsonWebKeyParameterNames.KeyType] = XkpKeyType.JwkKeyType.Name,
            [XkpKeyType.PublicMember] = Base64Url.EncodeToString(
                ((XkpSecretKey)secretKey).SubjectPublicKeyInfo
            ),
        };

    private static (byte[] SubjectPublicKeyInfo, int KeySizeBits) ReadSubjectPublicKeyInfo(
        byte[] subjectPublicKeyInfo
    )
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out _);
        return (subjectPublicKeyInfo, ecdsa.KeySize);
    }
}
