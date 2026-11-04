// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Keys;
using NCode.Identity.Jwk;

namespace NCode.Identity.Cose.Tests.Keys;

public class DefaultCoseKeyTypeCatalogTests
{
    private static readonly CoseKeyTypeDescriptor[] KeyTypes =
    [
        CoseKeyTypeDescriptors.Okp,
        CoseKeyTypeDescriptors.Ec2,
        CoseKeyTypeDescriptors.Rsa,
        CoseKeyTypeDescriptors.Symmetric,
        CoseKeyTypeDescriptors.Akp,
    ];

    private static readonly CoseEllipticCurve[] Curves =
    [
        CoseEllipticCurves.P256,
        CoseEllipticCurves.P384,
        CoseEllipticCurves.P521,
        CoseEllipticCurves.X25519,
        CoseEllipticCurves.X448,
        CoseEllipticCurves.Ed25519,
        CoseEllipticCurves.Ed448,
        CoseEllipticCurves.Secp256k1,
    ];

    private static CoseKeyTypeDescriptor Descriptor(
        int keyType,
        string jwkKeyType,
        params CoseKeyParameter[] parameters
    ) =>
        new()
        {
            KeyType = keyType,
            JwkKeyType = jwkKeyType,
            Parameters = parameters,
        };

    private static CoseKeyParameter Parameter(int label, string member = "x") =>
        new()
        {
            Label = label,
            JwkMember = member,
            Encoding = CoseKeyParameterEncoding.ByteString,
        };

    #region Constructor

    [Fact]
    public void Constructor_Defaults_Compose()
    {
        var catalog = new DefaultCoseKeyTypeCatalog(KeyTypes, Curves);

        Assert.Equal(5, catalog.KeyTypes.Count);
        Assert.Equal(8, catalog.Curves.Count);
    }

    [Fact]
    public void Constructor_NullKeyType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new DefaultCoseKeyTypeCatalog([null!], []));
    }

    [Fact]
    public void Constructor_NullCurve_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog(KeyTypes, [null!])
        );
    }

    [Fact]
    public void Constructor_EmptyJwkKeyType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog([Descriptor(50, " ")], [])
        );
    }

    [Fact]
    public void Constructor_EmptyJwkMember_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog([Descriptor(50, "X", Parameter(-1, ""))], [])
        );
    }

    [Fact]
    public void Constructor_DuplicateKeyType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog([Descriptor(50, "A"), Descriptor(50, "B")], [])
        );
    }

    [Fact]
    public void Constructor_DuplicateJwkKeyType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog([Descriptor(50, "A"), Descriptor(51, "A")], [])
        );
    }

    [Fact]
    public void Constructor_DuplicateParameterLabel_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog(
                [Descriptor(50, "A", Parameter(-1), Parameter(-1, "y"))],
                []
            )
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_CommonParameterLabel_Throws(int label)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog([Descriptor(50, "A", Parameter(label))], [])
        );
    }

    [Fact]
    public void Constructor_DuplicateCurveIdentifier_Throws()
    {
        var other = new CoseEllipticCurve
        {
            Identifier = 1,
            KeyType = CoseKeyTypeIdentifiers.Ec2,
            JwkCurveName = "other",
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog(KeyTypes, [CoseEllipticCurves.P256, other])
        );
    }

    [Fact]
    public void Constructor_DuplicateCurveName_Throws()
    {
        var other = new CoseEllipticCurve
        {
            Identifier = 99,
            KeyType = CoseKeyTypeIdentifiers.Ec2,
            JwkCurveName = "P-256",
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog(KeyTypes, [CoseEllipticCurves.P256, other])
        );
    }

    [Fact]
    public void Constructor_CurveOfUnregisteredKeyType_Throws()
    {
        var other = new CoseEllipticCurve
        {
            Identifier = 99,
            KeyType = 77,
            JwkCurveName = "other",
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultCoseKeyTypeCatalog(KeyTypes, [other])
        );
    }

    #endregion

    #region Lookups

    [Fact]
    public void TryGetKeyType_ByValueAndByJwkKeyType_FindTheDescriptor()
    {
        var catalog = new DefaultCoseKeyTypeCatalog(KeyTypes, Curves);

        Assert.True(catalog.TryGetKeyType(CoseKeyTypeIdentifiers.Ec2, out var byValue));
        Assert.Equal(JsonWebKeyTypes.Ec, byValue.JwkKeyType);
        Assert.True(catalog.TryGetKeyTypeByJwkKeyType(JsonWebKeyTypes.Okp, out var byJwk));
        Assert.Equal(CoseKeyTypeIdentifiers.Okp, byJwk.KeyType);
        Assert.False(catalog.TryGetKeyType(99, out _));
        Assert.False(catalog.TryGetKeyTypeByJwkKeyType("none", out _));
    }

    [Fact]
    public void TryGetCurve_ByIdentifierAndByName_FindTheCurve()
    {
        var catalog = new DefaultCoseKeyTypeCatalog(KeyTypes, Curves);

        Assert.True(catalog.TryGetCurve(CoseKeyTypeIdentifiers.Ec2, 1, out var byId));
        Assert.Equal("P-256", byId.JwkCurveName);
        Assert.True(
            catalog.TryGetCurveByJwkName(CoseKeyTypeIdentifiers.Okp, "Ed25519", out var byName)
        );
        Assert.Equal(CoseEllipticCurveIdentifiers.Ed25519, byName.Identifier);
        Assert.False(catalog.TryGetCurve(CoseKeyTypeIdentifiers.Okp, 1, out _));
        Assert.False(catalog.TryGetCurveByJwkName(CoseKeyTypeIdentifiers.Ec2, "Ed25519", out _));
    }

    #endregion
}
