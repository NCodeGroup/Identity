// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class OcspOpenSslInteropTests
{
    #region Encode Tests

    [Fact]
    public void Encode_ForTheOpenSslClientCertificate_MatchesTheRequestOpenSslWrote()
    {
        using var issuer = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(OcspOpenSslFixtures.IssuerCertificate)
        );
        using var client = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(OcspOpenSslFixtures.ClientCertificate)
        );
        var certificateId = OcspCertificateId.Create(client, issuer);

        var result = OcspRequest.Encode(certificateId);

        Assert.Equal(Convert.FromBase64String(OcspOpenSslFixtures.Request), result);
    }

    #endregion

    #region TryParse Tests

    [Fact]
    public void TryParse_AResponseSignedByTheIssuer_ReadsTheRevokedStatus()
    {
        using var issuer = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(OcspOpenSslFixtures.IssuerCertificate)
        );
        using var client = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(OcspOpenSslFixtures.ClientCertificate)
        );
        var certificateId = OcspCertificateId.Create(client, issuer);

        var result = OcspResponse.TryParse(
            Convert.FromBase64String(OcspOpenSslFixtures.ResponseSignedByIssuer),
            out var response
        );

        Assert.True(result);
        Assert.NotNull(response);
        Assert.Equal(0, response.ResponseStatus);
        Assert.False(response.HasUnsupportedFeatures);
        var single = response.FindResponse(certificateId);
        Assert.NotNull(single);
        Assert.Equal(ClientCertificateRevocationStatus.Revoked, single.Status);
        Assert.False(single.HasUnsupportedFeatures);
        Assert.Equal(OcspOpenSslFixtures.ProducedAt, single.ThisUpdate);
        Assert.NotNull(single.NextUpdate);
        Assert.Equal(OcspOpenSslFixtures.ProducedAt.AddDays(1), single.NextUpdate.Value);
    }

    #endregion

    #region TryVerifySignature Tests

    [Fact]
    public void TryVerifySignature_AResponseSignedByTheIssuer_ReturnsTrueWithNoResponderToCheck()
    {
        using var issuer = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(OcspOpenSslFixtures.IssuerCertificate)
        );
        var parsed = OcspResponse.TryParse(
            Convert.FromBase64String(OcspOpenSslFixtures.ResponseSignedByIssuer),
            out var response
        );
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            issuer,
            OcspOpenSslFixtures.ProducedAt,
            out var uncheckedResponder
        );

        Assert.True(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    [Fact]
    public void TryVerifySignature_AResponseFromADelegatedResponderWithNoCheck_ReturnsTrueWithNoResponderToCheck()
    {
        using var issuer = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(OcspOpenSslFixtures.IssuerCertificate)
        );
        using var client = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(OcspOpenSslFixtures.ClientCertificate)
        );
        var certificateId = OcspCertificateId.Create(client, issuer);
        var parsed = OcspResponse.TryParse(
            Convert.FromBase64String(OcspOpenSslFixtures.ResponseSignedByDelegatedResponder),
            out var response
        );
        Assert.True(parsed);
        Assert.NotNull(response);
        var single = response.FindResponse(certificateId);
        Assert.NotNull(single);
        Assert.Equal(ClientCertificateRevocationStatus.Revoked, single.Status);

        var result = response.TryVerifySignature(
            issuer,
            OcspOpenSslFixtures.ProducedAt,
            out var uncheckedResponder
        );

        Assert.True(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    [Fact]
    public void TryVerifySignature_WhenTheDelegatedResponderHasExpired_ReturnsFalse()
    {
        using var issuer = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(OcspOpenSslFixtures.IssuerCertificate)
        );
        var parsed = OcspResponse.TryParse(
            Convert.FromBase64String(OcspOpenSslFixtures.ResponseSignedByDelegatedResponder),
            out var response
        );
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            issuer,
            OcspOpenSslFixtures.ProducedAt.AddYears(11),
            out var uncheckedResponder
        );

        Assert.False(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    [Fact]
    public void TryVerifySignature_WithAnotherIssuer_ReturnsFalse()
    {
        using var responderCertificate = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(OcspOpenSslFixtures.ResponderCertificate)
        );
        var parsed = OcspResponse.TryParse(
            Convert.FromBase64String(OcspOpenSslFixtures.ResponseSignedByIssuer),
            out var response
        );
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            responderCertificate,
            OcspOpenSslFixtures.ProducedAt,
            out var uncheckedResponder
        );

        Assert.False(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    #endregion
}
