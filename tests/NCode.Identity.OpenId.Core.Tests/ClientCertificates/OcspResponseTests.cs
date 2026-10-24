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
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class OcspResponseTests
{
    private const string UnknownExtensionOid = "1.2.3.4";

    private static readonly byte[] NullValue = [0x05, 0x00];

    #region TryParse Tests

    [Fact]
    public void TryParse_AGoodResponse_ReadsItsFields()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var thisUpdate = DateTimeOffset.UtcNow.AddMinutes(-5);
        var nextUpdate = DateTimeOffset.UtcNow.AddDays(2);
        var encoded = TestOcspResponses.Build(
            leaf,
            root,
            root,
            thisUpdate: thisUpdate,
            nextUpdate: nextUpdate
        );

        var result = OcspResponse.TryParse(encoded, out var response);

        Assert.True(result);
        Assert.NotNull(response);
        Assert.Equal(0, response.ResponseStatus);
        var single = Assert.Single(response.Responses);
        Assert.True(single.CertificateId.Matches(OcspCertificateId.Create(leaf, root)));
        Assert.Equal(ClientCertificateRevocationStatus.Good, single.Status);
        Assert.Equal(thisUpdate.ToUnixTimeSeconds(), single.ThisUpdate.ToUnixTimeSeconds());
        Assert.NotNull(single.NextUpdate);
        Assert.Equal(nextUpdate.ToUnixTimeSeconds(), single.NextUpdate.Value.ToUnixTimeSeconds());
        Assert.False(single.HasUnsupportedFeatures);
        Assert.NotNull(response.ResponderName);
        Assert.Equal(root.SubjectName.RawData, response.ResponderName.RawData);
        Assert.True(response.ResponderKeyHash.IsEmpty);
        Assert.False(response.HasUnsupportedFeatures);
        Assert.False(response.SignedData.IsEmpty);
        Assert.False(response.Signature.IsEmpty);
        Assert.Equal("1.2.840.10045.4.3.2", response.SignatureAlgorithm);
    }

    [Fact]
    public void TryParse_ARevokedResponse_ReadsRevoked()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(
            leaf,
            root,
            root,
            ClientCertificateRevocationStatus.Revoked
        );

        var result = OcspResponse.TryParse(encoded, out var response);

        Assert.True(result);
        Assert.NotNull(response);
        var single = Assert.Single(response.Responses);
        Assert.Equal(ClientCertificateRevocationStatus.Revoked, single.Status);
    }

    [Fact]
    public void TryParse_AnUnknownResponse_ReadsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(
            leaf,
            root,
            root,
            ClientCertificateRevocationStatus.Unknown
        );

        var result = OcspResponse.TryParse(encoded, out var response);

        Assert.True(result);
        Assert.NotNull(response);
        var single = Assert.Single(response.Responses);
        Assert.Equal(ClientCertificateRevocationStatus.Unknown, single.Status);
    }

    [Fact]
    public void TryParse_WithoutNextUpdate_LeavesItNull()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, root, omitNextUpdate: true);

        var result = OcspResponse.TryParse(encoded, out var response);

        Assert.True(result);
        Assert.NotNull(response);
        var single = Assert.Single(response.Responses);
        Assert.Null(single.NextUpdate);
    }

    [Fact]
    public void TryParse_WithAResponderKeyHash_ReadsIt()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, root, responderByKey: true);

        var result = OcspResponse.TryParse(encoded, out var response);

        Assert.True(result);
        Assert.NotNull(response);
        Assert.Null(response.ResponderName);
        Assert.Equal(
            CryptographicOperations.HashData(
                HashAlgorithmName.SHA1,
                root.PublicKey.EncodedKeyValue.RawData
            ),
            response.ResponderKeyHash.ToArray()
        );
    }

    [Fact]
    public void TryParse_AnUnsuccessfulResponse_ReadsItsStatus()
    {
        var encoded = TestOcspResponses.Unsuccessful(3);

        var result = OcspResponse.TryParse(encoded, out var response);

        Assert.True(result);
        Assert.NotNull(response);
        Assert.Equal(3, response.ResponseStatus);
        Assert.Empty(response.Responses);
    }

    [Fact]
    public void TryParse_Garbage_ReturnsFalse()
    {
        var result = OcspResponse.TryParse(new byte[] { 0x01, 0x02, 0x03 }, out var response);

        Assert.False(result);
        Assert.Null(response);
    }

    [Fact]
    public void TryParse_WithACriticalResponseExtension_IsUnsupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(
            leaf,
            root,
            root,
            responseExtensions: [(UnknownExtensionOid, true, NullValue)]
        );

        var result = OcspResponse.TryParse(encoded, out var response);

        Assert.True(result);
        Assert.NotNull(response);
        Assert.True(response.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithANonCriticalNonce_IsSupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(
            leaf,
            root,
            root,
            responseExtensions: [(TestOcspResponses.NonceOid, false, [0x04, 0x02, 0x01, 0x02])]
        );

        var result = OcspResponse.TryParse(encoded, out var response);

        Assert.True(result);
        Assert.NotNull(response);
        Assert.False(response.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithACriticalSingleExtension_MarksTheSingleResponse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(
            leaf,
            root,
            root,
            singleExtensions: [(UnknownExtensionOid, true, NullValue)]
        );

        var result = OcspResponse.TryParse(encoded, out var response);

        Assert.True(result);
        Assert.NotNull(response);
        Assert.False(response.HasUnsupportedFeatures);
        var single = Assert.Single(response.Responses);
        Assert.True(single.HasUnsupportedFeatures);
    }

    #endregion

    #region FindResponse Tests

    [Fact]
    public void FindResponse_ForAnotherCertificate_ReturnsNull()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        using var other = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, root);
        var parsed = OcspResponse.TryParse(encoded, out var response);
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.FindResponse(OcspCertificateId.Create(other, root));

        Assert.Null(result);
    }

    #endregion

    #region TryVerifySignature Tests

    [Fact]
    public void TryVerifySignature_WhenTheIssuerSigned_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, root);
        var parsed = OcspResponse.TryParse(encoded, out var response);
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            root,
            DateTimeOffset.UtcNow,
            out var uncheckedResponder
        );

        Assert.True(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    [Fact]
    public void TryVerifySignature_WhenTheIssuerSignedWithAKeyHash_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, root, responderByKey: true);
        var parsed = OcspResponse.TryParse(encoded, out var response);
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            root,
            DateTimeOffset.UtcNow,
            out var uncheckedResponder
        );

        Assert.True(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    [Fact]
    public void TryVerifySignature_WhenAnotherAuthoritySigned_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var otherRoot = TestCertificates.CreateRootAuthority("Other Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, otherRoot);
        var parsed = OcspResponse.TryParse(encoded, out var response);
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            root,
            DateTimeOffset.UtcNow,
            out var uncheckedResponder
        );

        Assert.False(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    [Fact]
    public void TryVerifySignature_WhenADelegatedResponderWithNoCheckSigned_ReturnsTrueWithoutAResponderToCheck()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var responder = TestCertificates.CreateOcspResponder(
            root,
            "Responder",
            noCheck: true
        );
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, responder, certificates: [responder]);
        var parsed = OcspResponse.TryParse(encoded, out var response);
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            root,
            DateTimeOffset.UtcNow,
            out var uncheckedResponder
        );

        Assert.True(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    [Fact]
    public void TryVerifySignature_WhenADelegatedResponderWithoutNoCheckSigned_ReturnsTheResponderToCheck()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var responder = TestCertificates.CreateOcspResponder(
            root,
            "Responder",
            noCheck: false
        );
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, responder, certificates: [responder]);
        var parsed = OcspResponse.TryParse(encoded, out var response);
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            root,
            DateTimeOffset.UtcNow,
            out var uncheckedResponder
        );

        Assert.True(result);
        Assert.Equal(responder.RawData, uncheckedResponder.ToArray());
    }

    [Fact]
    public void TryVerifySignature_WhenTheSignerLacksOcspSigning_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var signer = TestCertificates.CreateClientCertificate(root, "CN=signer");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, signer, certificates: [signer]);
        var parsed = OcspResponse.TryParse(encoded, out var response);
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            root,
            DateTimeOffset.UtcNow,
            out var uncheckedResponder
        );

        Assert.False(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    [Fact]
    public void TryVerifySignature_WhenAnotherAuthorityIssuedTheResponder_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var otherRoot = TestCertificates.CreateRootAuthority("Other Root");
        using var responder = TestCertificates.CreateOcspResponder(
            otherRoot,
            "Responder",
            noCheck: true
        );
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, responder, certificates: [responder]);
        var parsed = OcspResponse.TryParse(encoded, out var response);
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            root,
            DateTimeOffset.UtcNow,
            out var uncheckedResponder
        );

        Assert.False(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    [Fact]
    public void TryVerifySignature_WhenTheResponderHasExpired_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var responder = TestCertificates.CreateOcspResponder(
            root,
            "Responder",
            noCheck: true
        );
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = TestOcspResponses.Build(leaf, root, responder, certificates: [responder]);
        var parsed = OcspResponse.TryParse(encoded, out var response);
        Assert.True(parsed);
        Assert.NotNull(response);

        var result = response.TryVerifySignature(
            root,
            DateTimeOffset.UtcNow.AddYears(2),
            out var uncheckedResponder
        );

        Assert.False(result);
        Assert.True(uncheckedResponder.IsEmpty);
    }

    #endregion
}
