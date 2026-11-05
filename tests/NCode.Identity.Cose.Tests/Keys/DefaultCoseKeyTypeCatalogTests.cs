// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Keys;
using NCode.Identity.Jwk;

namespace NCode.Identity.Cose.Tests.Keys;

public class DefaultCoseKeyTypeCatalogTests
{
    private static CoseKeyTypeDescriptor Descriptor(
        int keyType,
        string jwkKeyType,
        params CoseKeyParameter[] parameters
    ) =>
        new()
        {
            KeyType = keyType,
            JwkKeyType = jwkKeyType,
            Parameters = [.. parameters],
            RequiresAlgorithm = false,
        };

    private static CoseKeyParameter Parameter(int label, string member = "x") =>
        new()
        {
            Label = label,
            JwkMember = member,
            Encoding = CoseKeyParameterEncoding.ByteString,
            IsPrivate = false,
        };

    #region Constructor Tests

    [Fact]
    public void Constructor_Defaults_Compose()
    {
        var catalog = new DefaultCoseKeyTypeCatalog(
            CoseTestCatalogs.KeyTypes,
            CoseTestCatalogs.Curves
        );

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
            new DefaultCoseKeyTypeCatalog(CoseTestCatalogs.KeyTypes, [null!])
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
            new DefaultCoseKeyTypeCatalog(
                CoseTestCatalogs.KeyTypes,
                [CoseEllipticCurves.P256, other]
            )
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
            new DefaultCoseKeyTypeCatalog(
                CoseTestCatalogs.KeyTypes,
                [CoseEllipticCurves.P256, other]
            )
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
            new DefaultCoseKeyTypeCatalog(CoseTestCatalogs.KeyTypes, [other])
        );
    }

    #endregion

    #region TryGetKeyType Tests

    [Fact]
    public void TryGetKeyType_ByValueAndByJwkKeyType_FindTheDescriptor()
    {
        var catalog = new DefaultCoseKeyTypeCatalog(
            CoseTestCatalogs.KeyTypes,
            CoseTestCatalogs.Curves
        );

        Assert.True(catalog.TryGetKeyType(CoseKeyTypeIdentifiers.Ec2, out var byValue));
        Assert.Equal(JsonWebKeyTypes.Ec, byValue.JwkKeyType);
        Assert.True(catalog.TryGetKeyTypeByJwkKeyType(JsonWebKeyTypes.Okp, out var byJwk));
        Assert.Equal(CoseKeyTypeIdentifiers.Okp, byJwk.KeyType);
        Assert.False(catalog.TryGetKeyType(99, out _));
        Assert.False(catalog.TryGetKeyTypeByJwkKeyType("none", out _));
    }

    #endregion

    #region TryGetCurve Tests

    [Fact]
    public void TryGetCurve_ByIdentifierAndByName_FindTheCurve()
    {
        var catalog = new DefaultCoseKeyTypeCatalog(
            CoseTestCatalogs.KeyTypes,
            CoseTestCatalogs.Curves
        );

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
