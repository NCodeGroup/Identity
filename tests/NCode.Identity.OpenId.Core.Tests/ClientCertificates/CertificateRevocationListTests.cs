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

using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class CertificateRevocationListTests
{
    private const string UnknownExtensionOid = "1.2.3.4";

    private static readonly byte[] NullValue = [0x05, 0x00];

    private static byte[] BuildCrl(
        X509Certificate2 root,
        DateTimeOffset nextUpdate,
        params X509Certificate2[] revoked
    )
    {
        var builder = new CertificateRevocationListBuilder();
        foreach (var certificate in revoked)
            builder.AddEntry(certificate);

        return builder.Build(root, BigInteger.One, nextUpdate, HashAlgorithmName.SHA256);
    }

    #region TryParse Tests

    [Fact]
    public void TryParse_ABuiltCrl_ReadsItsFields()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var nextUpdate = DateTimeOffset.UtcNow.AddDays(1);
        var encoded = BuildCrl(root, nextUpdate, leaf);

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.Equal(root.SubjectName.RawData, crl.Issuer.RawData);
        Assert.InRange(
            crl.ThisUpdate,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        Assert.NotNull(crl.NextUpdate);
        Assert.Equal(nextUpdate.ToUnixTimeSeconds(), crl.NextUpdate.Value.ToUnixTimeSeconds());
        Assert.Single(crl.EntriesBySerialNumber);
        var entry = crl.FindEntry(
            new BigInteger(leaf.SerialNumberBytes.Span, isUnsigned: false, isBigEndian: true),
            root.SubjectName
        );
        Assert.NotNull(entry);
        Assert.False(entry.IsRemoveFromCrl);
        Assert.False(crl.HasUnsupportedFeatures);
        Assert.Null(crl.IssuingDistributionPoint);
        Assert.Equal("1.2.840.10045.4.3.2", crl.SignatureAlgorithm);
        Assert.False(crl.SignedData.IsEmpty);
        Assert.False(crl.Signature.IsEmpty);
    }

    [Fact]
    public void TryParse_Garbage_ReturnsFalse()
    {
        var result = CertificateRevocationList.TryParse(
            new byte[] { 0x01, 0x02, 0x03 },
            out var crl
        );

        Assert.False(result);
        Assert.Null(crl);
    }

    [Fact]
    public void TryParse_WithoutNextUpdate_LeavesItNull()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(root, omitNextUpdate: true);

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.Null(crl.NextUpdate);
    }

    [Fact]
    public void TryParse_WithAnUnknownCriticalExtension_IsUnsupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(root, extensions: [(UnknownExtensionOid, true, NullValue)]);

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.True(crl.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithAnUnknownNonCriticalExtension_IsSupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(root, extensions: [(UnknownExtensionOid, false, NullValue)]);

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.False(crl.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithADeltaCrlIndicator_ReadsTheBaseCrlNumber()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            extensions: [(TestCrls.DeltaCrlIndicatorOid, false, new byte[] { 0x02, 0x01, 0x01 })]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.False(crl.HasUnsupportedFeatures);
        Assert.True(crl.IsDelta);
        Assert.Equal(BigInteger.One, crl.DeltaBaseCrlNumber);
    }

    [Fact]
    public void TryParse_ABuiltCrl_ReadsItsCrlNumberAndAuthorityKey()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1));

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.False(crl.HasUnsupportedFeatures);
        Assert.Equal(BigInteger.One, crl.CrlNumber);
        Assert.False(crl.AuthorityKeyIdentifier.IsEmpty);
        Assert.False(crl.IsDelta);
        Assert.Null(crl.DeltaBaseCrlNumber);
    }

    [Fact]
    public void TryParse_WithAFreshestCrl_ReadsItsLocations()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var freshest = TestCrls.DistributionPoints(
            TestCrls.FreshestCrlOid,
            new TestDistributionPoint("http://crl.example.test/delta.crl")
        );
        var encoded = TestCrls.Build(
            root,
            extensions: [(TestCrls.FreshestCrlOid, false, freshest.RawData)]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.False(crl.HasUnsupportedFeatures);
        Assert.Equal(["http://crl.example.test/delta.crl"], crl.FreshestCrlUris);
    }

    [Fact]
    public void TryParse_WithACriticalEntryExtension_IsUnsupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            revokedSerialNumbers: [new byte[] { 0x01, 0x02 }],
            entryExtensions: [(UnknownExtensionOid, true, NullValue)]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.True(crl.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithAnIssuingDistributionPoint_ReadsItsUri()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        const string uri = "http://crl.example.com/root.crl";
        var encoded = TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(uri: uri, onlyContainsUserCertificates: true)
                ),
            ]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.False(crl.HasUnsupportedFeatures);
        Assert.NotNull(crl.IssuingDistributionPoint);
        Assert.Equal(uri, Assert.Single(crl.IssuingDistributionPoint.Uris));
        Assert.True(crl.IssuingDistributionPoint.HasName);
        Assert.True(crl.IssuingDistributionPoint.OnlyContainsUserCertificates);
        Assert.False(crl.IssuingDistributionPoint.OnlyContainsAuthorityCertificates);
    }

    [Fact]
    public void TryParse_WithAnIndirectIssuingDistributionPoint_ReadsIt()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(indirect: true)
                ),
            ]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.False(crl.HasUnsupportedFeatures);
        Assert.NotNull(crl.IssuingDistributionPoint);
        Assert.True(crl.IssuingDistributionPoint.IsIndirect);
        Assert.False(crl.IssuingDistributionPoint.HasName);
    }

    [Fact]
    public void TryParse_WithOnlySomeReasons_ReadsThem()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(onlySomeReasons: 0x006)
                ),
            ]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.False(crl.HasUnsupportedFeatures);
        Assert.NotNull(crl.IssuingDistributionPoint);
        Assert.Equal(0x006, crl.IssuingDistributionPoint.OnlySomeReasons);
    }

    [Fact]
    public void TryParse_WithOnlyAttributeCertificates_IsUnsupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(onlyContainsAttributeCertificates: true)
                ),
            ]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.True(crl.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithACertificateIssuerInADirectList_IsUnsupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            entries:
            [
                new TestCrlEntry(
                    [0x01],
                    [
                        (
                            TestCrls.CertificateIssuerOid,
                            true,
                            TestCrls.CertificateIssuer(new X500DistinguishedName("CN=Other Issuer"))
                        ),
                    ]
                ),
            ]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.True(crl.HasUnsupportedFeatures);
    }

    #endregion

    #region FindEntry Tests

    private static readonly X500DistinguishedName OtherIssuer = new("CN=Other Issuer");

    private static byte[] BuildIndirectCrl(X509Certificate2 root, params TestCrlEntry[] entries) =>
        TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(indirect: true)
                ),
            ],
            entries: entries
        );

    private static CertificateRevocationList Parse(byte[] encoded)
    {
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);
        return crl;
    }

    [Fact]
    public void FindEntry_WhenTheSerialIsListed_ReturnsTheEntry()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var crl = Parse(BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1), leaf));
        var serialNumber = new BigInteger(
            leaf.SerialNumberBytes.Span,
            isUnsigned: false,
            isBigEndian: true
        );

        var result = crl.FindEntry(serialNumber, root.SubjectName);

        Assert.NotNull(result);
        Assert.Equal(serialNumber, result.SerialNumber);
        Assert.Equal(root.SubjectName.RawData, result.CertificateIssuer.RawData);
        Assert.False(result.IsRemoveFromCrl);
    }

    [Fact]
    public void FindEntry_WhenTheSerialIsNotListed_ReturnsNull()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        using var other = TestCertificates.CreateClientCertificate(root, "CN=d");
        var crl = Parse(BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1), leaf));
        var serialNumber = new BigInteger(
            other.SerialNumberBytes.Span,
            isUnsigned: false,
            isBigEndian: true
        );

        var result = crl.FindEntry(serialNumber, root.SubjectName);

        Assert.Null(result);
    }

    [Fact]
    public void FindEntry_WhenACertificateIssuerNamesAnotherIssuer_ReturnsNull()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = Parse(
            BuildIndirectCrl(
                root,
                new TestCrlEntry(
                    [0x01],
                    [(TestCrls.CertificateIssuerOid, true, TestCrls.CertificateIssuer(OtherIssuer))]
                )
            )
        );

        var result = crl.FindEntry(BigInteger.One, root.SubjectName);

        Assert.False(crl.HasUnsupportedFeatures);
        Assert.Null(result);
        Assert.NotNull(crl.FindEntry(BigInteger.One, OtherIssuer));
    }

    [Fact]
    public void FindEntry_WithACertificateIssuer_AppliesItToTheEntriesAfterIt()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = Parse(
            BuildIndirectCrl(
                root,
                new TestCrlEntry([0x01]),
                new TestCrlEntry(
                    [0x02],
                    [(TestCrls.CertificateIssuerOid, true, TestCrls.CertificateIssuer(OtherIssuer))]
                ),
                new TestCrlEntry([0x03])
            )
        );

        Assert.NotNull(crl.FindEntry(new BigInteger(1), root.SubjectName));
        Assert.Null(crl.FindEntry(new BigInteger(1), OtherIssuer));
        Assert.NotNull(crl.FindEntry(new BigInteger(2), OtherIssuer));
        Assert.NotNull(crl.FindEntry(new BigInteger(3), OtherIssuer));
        Assert.Null(crl.FindEntry(new BigInteger(3), root.SubjectName));
    }

    [Fact]
    public void FindEntry_WithRemoveFromCrl_MarksTheEntry()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = Parse(
            TestCrls.Build(
                root,
                entries:
                [
                    new TestCrlEntry(
                        [0x01],
                        [(TestCrls.ReasonCodeOid, false, TestCrls.ReasonCode(8))]
                    ),
                    new TestCrlEntry(
                        [0x02],
                        [(TestCrls.ReasonCodeOid, false, TestCrls.ReasonCode(1))]
                    ),
                ]
            )
        );

        var removed = crl.FindEntry(new BigInteger(1), root.SubjectName);
        var revoked = crl.FindEntry(new BigInteger(2), root.SubjectName);

        Assert.NotNull(removed);
        Assert.True(removed.IsRemoveFromCrl);
        Assert.NotNull(revoked);
        Assert.False(revoked.IsRemoveFromCrl);
    }

    #endregion

    #region CanApplyDelta Tests

    private static readonly byte[] AuthorityKey = [0x30, 0x04, 0x80, 0x02, 0x01, 0x02];

    private static CertificateRevocationList ParseNumbered(
        X509Certificate2 issuer,
        int number,
        int? deltaBase = null,
        string? issuingDistributionPointUri = null
    )
    {
        var extensions = new List<(string Oid, bool Critical, byte[] Value)>
        {
            (TestCrls.CrlNumberOid, false, TestCrls.CrlNumber(number)),
            (TestCrls.AuthorityKeyIdentifierOid, false, AuthorityKey),
        };
        if (deltaBase is { } baseNumber)
        {
            extensions.Add(
                (TestCrls.DeltaCrlIndicatorOid, true, TestCrls.DeltaCrlIndicator(baseNumber))
            );
        }

        if (issuingDistributionPointUri is not null)
        {
            extensions.Add(
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(uri: issuingDistributionPointUri)
                )
            );
        }

        return Parse(TestCrls.Build(issuer, extensions: extensions));
    }

    [Fact]
    public void CanApplyDelta_WhenTheNumbersAndScopeMatch_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var complete = ParseNumbered(root, number: 10);
        var delta = ParseNumbered(root, number: 11, deltaBase: 10);

        var result = complete.CanApplyDelta(delta);

        Assert.True(result);
    }

    [Fact]
    public void CanApplyDelta_WhenTheBaseIsAfterTheCompleteList_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var complete = ParseNumbered(root, number: 10);
        var delta = ParseNumbered(root, number: 12, deltaBase: 11);

        var result = complete.CanApplyDelta(delta);

        Assert.False(result);
    }

    [Fact]
    public void CanApplyDelta_WhenTheDeltaIsNotAfterTheCompleteList_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var complete = ParseNumbered(root, number: 10);
        var delta = ParseNumbered(root, number: 10, deltaBase: 9);

        var result = complete.CanApplyDelta(delta);

        Assert.False(result);
    }

    [Fact]
    public void CanApplyDelta_WhenTheIssuersDiffer_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var other = TestCertificates.CreateRootAuthority("Other");
        var complete = ParseNumbered(root, number: 10);
        var delta = ParseNumbered(other, number: 11, deltaBase: 10);

        var result = complete.CanApplyDelta(delta);

        Assert.False(result);
    }

    [Fact]
    public void CanApplyDelta_WhenTheIssuingDistributionPointsDiffer_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var complete = ParseNumbered(
            root,
            number: 10,
            issuingDistributionPointUri: "http://crl.example.test/a.crl"
        );
        var delta = ParseNumbered(
            root,
            number: 11,
            deltaBase: 10,
            issuingDistributionPointUri: "http://crl.example.test/b.crl"
        );

        var result = complete.CanApplyDelta(delta);

        Assert.False(result);
    }

    #endregion

    #region IsInScope Tests

    private const string PointUri = "http://crl.example.test/a.crl";

    private static CertificateRevocationList ParseWithExtension(
        X509Certificate2 issuer,
        byte[] issuingDistributionPoint
    ) =>
        Parse(
            TestCrls.Build(
                issuer,
                extensions: [(TestCrls.IssuingDistributionPointOid, true, issuingDistributionPoint)]
            )
        );

    [Fact]
    public void IsInScope_ForADirectListOfTheIssuer_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = Parse(BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1)));
        var point = new CrlDistributionPoint([PointUri], null, null);

        var result = crl.IsInScope(point, root.SubjectName, isAuthority: false);

        Assert.True(result);
    }

    [Fact]
    public void IsInScope_WhenTheIssuerDiffers_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = Parse(BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1)));
        var point = new CrlDistributionPoint([PointUri], null, null);

        var result = crl.IsInScope(point, OtherIssuer, isAuthority: false);

        Assert.False(result);
    }

    [Fact]
    public void IsInScope_WhenThePointNamesACrlIssuerAndTheListIsIndirect_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = Parse(BuildIndirectCrl(root));
        var point = new CrlDistributionPoint([PointUri], null, root.SubjectName);

        var result = crl.IsInScope(point, OtherIssuer, isAuthority: false);

        Assert.True(result);
    }

    [Fact]
    public void IsInScope_WhenThePointNamesACrlIssuerAndTheListIsDirect_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = Parse(BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1)));
        var point = new CrlDistributionPoint([PointUri], null, root.SubjectName);

        var result = crl.IsInScope(point, root.SubjectName, isAuthority: false);

        Assert.False(result);
    }

    [Fact]
    public void IsInScope_WhenTheIssuingDistributionPointNamesAnotherLocation_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = ParseWithExtension(root, TestCrls.IssuingDistributionPoint(uri: PointUri));
        var matching = new CrlDistributionPoint([PointUri], null, null);
        var other = new CrlDistributionPoint(["http://crl.example.test/b.crl"], null, null);

        var result = crl.IsInScope(other, root.SubjectName, isAuthority: false);

        Assert.False(result);
        Assert.True(crl.IsInScope(matching, root.SubjectName, isAuthority: false));
    }

    [Fact]
    public void IsInScope_WhenOnlyUserCertificatesAndTheCertificateIsAnAuthority_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = ParseWithExtension(
            root,
            TestCrls.IssuingDistributionPoint(onlyContainsUserCertificates: true)
        );
        var point = new CrlDistributionPoint([PointUri], null, null);

        var result = crl.IsInScope(point, root.SubjectName, isAuthority: true);

        Assert.False(result);
        Assert.True(crl.IsInScope(point, root.SubjectName, isAuthority: false));
    }

    #endregion

    #region GetCoveredReasons Tests

    [Fact]
    public void GetCoveredReasons_WithoutReasons_ReturnsEveryReason()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = Parse(BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1)));
        var point = new CrlDistributionPoint([PointUri], null, null);

        var result = crl.GetCoveredReasons(point);

        Assert.Equal(0x1FE, result);
    }

    [Fact]
    public void GetCoveredReasons_WithBoth_ReturnsTheIntersection()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = ParseWithExtension(
            root,
            TestCrls.IssuingDistributionPoint(onlySomeReasons: 0x006)
        );
        var point = new CrlDistributionPoint([PointUri], 0x00E, null);

        var result = crl.GetCoveredReasons(point);

        Assert.Equal(0x006, result);
    }

    [Fact]
    public void GetCoveredReasons_WithOnlyThePointsReasons_ReturnsThem()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var crl = Parse(BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1)));
        var point = new CrlDistributionPoint([PointUri], 0x00E, null);

        var result = crl.GetCoveredReasons(point);

        Assert.Equal(0x00E, result);
    }

    #endregion

    #region IsSignedBy Tests

    private static X509Certificate2 CreateAuthority(
        string commonName,
        X509KeyUsageFlags keyUsage,
        bool rsa = false
    )
    {
        using var rsaKey = rsa ? RSA.Create(2048) : null;
        using var ecdsaKey = rsa ? null : ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = rsaKey is not null
            ? new CertificateRequest(
                $"CN={commonName}",
                rsaKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            )
            : new CertificateRequest($"CN={commonName}", ecdsaKey!, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false)
        );
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );
    }

    [Fact]
    public void IsSignedBy_TheIssuingAuthority_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1), leaf);
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsSignedBy(root);

        Assert.True(result);
    }

    [Fact]
    public void IsSignedBy_AnRsaAuthority_ReturnsTrue()
    {
        using var root = CreateAuthority(
            "RsaRoot",
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            rsa: true
        );
        var encoded = new CertificateRevocationListBuilder().Build(
            root,
            BigInteger.One,
            DateTimeOffset.UtcNow.AddDays(1),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);
        Assert.Equal("1.2.840.113549.1.1.11", crl.SignatureAlgorithm);

        var result = crl.IsSignedBy(root);

        Assert.True(result);
    }

    [Theory]
    [InlineData("SHA256")]
    [InlineData("SHA384")]
    [InlineData("SHA512")]
    public void IsSignedBy_AnRsaPssAuthority_ReturnsTrue(string hashAlgorithmName)
    {
        using var root = CreateAuthority(
            "RsaPssRoot",
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            rsa: true
        );
        var encoded = new CertificateRevocationListBuilder().Build(
            root,
            BigInteger.One,
            DateTimeOffset.UtcNow.AddDays(1),
            new HashAlgorithmName(hashAlgorithmName),
            RSASignaturePadding.Pss
        );
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);
        Assert.Equal("1.2.840.113549.1.1.10", crl.SignatureAlgorithm);

        var result = crl.IsSignedBy(root);

        Assert.True(result);
    }

    [Fact]
    public void IsSignedBy_AnotherAuthority_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var other = TestCertificates.CreateRootAuthority("Other");
        var encoded = BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1));
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsSignedBy(other);

        Assert.False(result);
    }

    [Fact]
    public void IsSignedBy_AnAuthorityWithoutCrlSign_ReturnsFalse()
    {
        using var authority = CreateAuthority("NoCrlSign", X509KeyUsageFlags.KeyCertSign);
        var encoded = TestCrls.Build(authority);
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsSignedBy(authority);

        Assert.False(result);
    }

    [Fact]
    public void IsSignedBy_WhenTheSignatureIsAltered_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1), leaf);
        encoded[^1] ^= 0xFF;
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsSignedBy(root);

        Assert.False(result);
    }

    #endregion
}
