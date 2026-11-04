// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Cose.Tests;

/// <summary>
/// Builds the keys of the test vectors from their hexadecimal material.
/// </summary>
public static class CoseTestKeys
{
    public const string EcP256X =
        "BAC5B11CAD8F99F9C72B05CF4B9E26D244DC189F745228255A219A86D6A09EFF";
    public const string EcP256Y =
        "20138BF82DC1B6D562BE0FA54AB7804A3A64B6D72CCFED6B6FB6ED28BBFC117E";
    public const string EcP256D =
        "57C92077664146E876760C9520D054AA93C3AFB04E306705DB6090308507B4D3";

    public const string CwtX = "143329CCE7868E416927599CF65A34F3CE2FFDA55A7ECA69ED8919A394D42F0F";
    public const string CwtY = "60F7F1A780D8A783BFB7A2DD6B2796E8128DBBCEF9D3D168DB9529971A36E7B9";
    public const string CwtD = "6C1382765AEC5358F117733D281C1C7BDC39884D04A45A1E6C67C858BC206C19";

    public const string Ed25519Seed =
        "9D61B19DEFFD5A60BA844AF492EC2CC44449C5697B326919703BAC031CAE7F60";
    public const string Ed25519Public =
        "D75A980182B10AB7D54BFED3C964073A0EE172F3DAA62325AF021A68F707511A";

    public const string RsaN =
        "BC7E29D0DF7E20CC9DC8D509E0F68895922AF0EF452190D402C61B554334A7BF91C9A570240F994FAE1B69035BCFAD4F7E249EB26087C2665E7C958C967B1517413DC3F97A431691A5999B257CC6CD356BAD168D929B8BAE9020750E74CF60F6FD35D6BB3FC93FC28900478694F508B33E7C00E24F90EDF37457FC3E8EFCFD2F42306301A8205AB740515331D5C18F0C64D4A43BE52FC440400F6BFC558A6E32884C2AF56F29E5C52780CEA7285F5C057FC0DFDA232D0ADA681B01495D9D0E32196633588E289E59035FF664F056189F2F10FE05827B796C326E3E748FFA7C589ED273C9C43436CDDB4A6A22523EF8BCB2221615B799966F1ABA5BC84B7A27CF";
    public const string RsaE = "010001";

    public const string HmacKey =
        "849B57219DAE48DE646D07DBB533566E976686457C1491BE3A76DCEA6C427188";
    public const string AesKey = "849B57219DAE48DE646D07DBB533566E";
    public const string HmacCwtKey =
        "403697DE87AF64611C1D32A05DAB0FE1FCB715A86AB435F1EC99192D79569388";

    public const string Content = "546869732069732074686520636F6E74656E742E";

    public static byte[] Hex(string hex) => Convert.FromHexString(hex);

    /// <summary>
    /// Creates a P-256 key; with <paramref name="d"/> the key holds its private half.
    /// </summary>
    public static EccSecretKey CreateEcP256(
        ISecretKeyFactory factory,
        string x,
        string y,
        string? d = null,
        string? keyId = null
    )
    {
        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Hex(x), Y = Hex(y) },
            D = d is null ? null : Hex(d),
        };
        var metadata = new KeyMetadata { KeyId = keyId };
        var key = ECDsa.Create(parameters);
        return d is null
            ? factory.CreateEccPublicKey(metadata, key)
            : factory.CreateEcc(metadata, key);
    }

    /// <summary>
    /// Creates a P-256 key with the metadata given.
    /// </summary>
    public static EccSecretKey CreateEcP256(
        ISecretKeyFactory factory,
        string x,
        string y,
        KeyMetadata metadata,
        string? d = null
    )
    {
        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Hex(x), Y = Hex(y) },
            D = d is null ? null : Hex(d),
        };
        var key = ECDsa.Create(parameters);
        return d is null
            ? factory.CreateEccPublicKey(metadata, key)
            : factory.CreateEcc(metadata, key);
    }

    public static RsaSecretKey CreateRsaPublic(
        ISecretKeyFactory factory,
        string n,
        string e,
        string? keyId = null
    )
    {
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = Hex(n), Exponent = Hex(e) });
        return factory.CreateRsaPublicKey(new KeyMetadata { KeyId = keyId }, rsa);
    }

    public static SymmetricSecretKey CreateSymmetric(
        ISecretKeyFactory factory,
        string hex,
        string? keyId = null
    ) => factory.CreateSymmetric(new KeyMetadata { KeyId = keyId }, Hex(hex));

    public static SecretKey CreateEd25519(
        ISecretKeyTypeCatalog catalog,
        string seedHex,
        string? keyId = null
    )
    {
        Assert.True(catalog.TryGetByJwkKeyType("OKP", out var descriptor));
        var pkcs8 = Hex("302E020100300506032B657004220420").Concat(Hex(seedHex)).ToArray();
        return descriptor.DeserializeKeyMaterial(
            new KeyMetadata { KeyId = keyId },
            SecretEncodings.Pkcs8,
            pkcs8
        );
    }
}
