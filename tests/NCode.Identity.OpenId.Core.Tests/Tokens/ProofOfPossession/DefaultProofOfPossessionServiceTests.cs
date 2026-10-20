#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Encoders;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.ProofOfPossession;

public class DefaultProofOfPossessionServiceTests : BaseTests
{
    private Mock<IDPoPProofValidator> MockValidator { get; }
    private Mock<IClientCertificateResolver> MockCertificateResolver { get; }
    private Mock<ISecurityProfileCatalog> MockSecurityProfileCatalog { get; }
    private Mock<IReadOnlySettingCollection> MockSettings { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdClient> MockClient { get; }
    private DefaultHttpContext HttpContext { get; }

    private DefaultProofOfPossessionService Service { get; }

    public DefaultProofOfPossessionServiceTests()
    {
        MockValidator = CreateStrictMock<IDPoPProofValidator>();
        MockCertificateResolver = CreateStrictMock<IClientCertificateResolver>();
        MockSecurityProfileCatalog = CreateLooseMock<ISecurityProfileCatalog>();
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockClient = CreateStrictMock<OpenIdClient>();

        HttpContext = new DefaultHttpContext();
        HttpContext.Request.Scheme = "https";
        HttpContext.Request.Host = new HostString("op.test");
        HttpContext.Request.Path = "/token";
        HttpContext.Request.Method = "POST";

        MockContext.SetupGet(x => x.Http).Returns(HttpContext);
        MockClient.SetupGet(x => x.Settings).Returns(MockSettings.Object);

        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);

        Service = new DefaultProofOfPossessionService(
            MockValidator.Object,
            MockCertificateResolver.Object,
            MockSecurityProfileCatalog.Object
        );
    }

    #region Scaffolding

    private void SetupSettings(bool requireDPoP, bool certificateBound)
    {
        MockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.RequireDPoP)).Returns(requireDPoP);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TlsClientCertificateBoundAccessTokens))
            .Returns(certificateBound);
    }

    private void SetupSenderConstraintRequired() =>
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.AccessTokenSenderConstraintRequired))
            .Returns(true)
            .Verifiable();

    #endregion

    #region EvaluateTokenRequestAsync Tests

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenNoBinding_ReturnsUnbound()
    {
        SetupSettings(requireDPoP: false, certificateBound: false);

        var binding = await Service.EvaluateTokenRequestAsync(
            MockContext.Object,
            MockClient.Object,
            CancellationToken.None
        );

        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, binding.TokenType);
        Assert.False(binding.IsSenderConstrained);
    }

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenDPoPProofPresent_ReturnsDPoPBinding()
    {
        SetupSettings(requireDPoP: false, certificateBound: false);
        HttpContext.Request.Headers[OpenIdConstants.DPoP.HeaderName] = "proof-jwt";
        MockValidator
            .Setup(x =>
                x.ValidateAsync(
                    MockContext.Object,
                    It.Is<DPoPProofValidationRequest>(r =>
                        r.ProofJwt == "proof-jwt"
                        && r.HttpMethod == "POST"
                        && r.HttpUri == "https://op.test/token"
                        && r.Settings == MockSettings.Object
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("jkt-thumbprint")
            .Verifiable();

        var binding = await Service.EvaluateTokenRequestAsync(
            MockContext.Object,
            MockClient.Object,
            CancellationToken.None
        );

        Assert.Equal(OpenIdConstants.TokenTypes.DPoP, binding.TokenType);
        Assert.Equal("jkt-thumbprint", binding.Confirmation.JwkThumbprint);
    }

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenDPoPRequiredAndMissing_Throws()
    {
        SetupSettings(requireDPoP: true, certificateBound: false);

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Service
                .EvaluateTokenRequestAsync(
                    MockContext.Object,
                    MockClient.Object,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenCertificateBound_ReturnsCertificateConfirmation()
    {
        SetupSettings(requireDPoP: false, certificateBound: true);
        using var certificate = CreateSelfSignedCertificate();
        MockCertificateResolver
            .Setup(x =>
                x.ResolveCertificateAsync(MockContext.Object, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new PresentedClientCertificate(certificate))
            .Verifiable();

        var binding = await Service.EvaluateTokenRequestAsync(
            MockContext.Object,
            MockClient.Object,
            CancellationToken.None
        );

        var expected = Base64Url.Encode(SHA256.HashData(certificate.RawData));
        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, binding.TokenType);
        Assert.Equal(expected, binding.Confirmation.X509CertificateSha256Thumbprint);
    }

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenCertificateBoundButMissing_Throws()
    {
        SetupSettings(requireDPoP: false, certificateBound: true);
        MockCertificateResolver
            .Setup(x =>
                x.ResolveCertificateAsync(MockContext.Object, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PresentedClientCertificate?)null)
            .Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Service
                .EvaluateTokenRequestAsync(
                    MockContext.Object,
                    MockClient.Object,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenSenderConstraintRequiredAndUnbound_Throws()
    {
        SetupSettings(requireDPoP: false, certificateBound: false);
        SetupSenderConstraintRequired();

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Service
                .EvaluateTokenRequestAsync(
                    MockContext.Object,
                    MockClient.Object,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenSenderConstraintRequiredAndDPoPBound_ReturnsBinding()
    {
        SetupSettings(requireDPoP: false, certificateBound: false);
        SetupSenderConstraintRequired();
        HttpContext.Request.Headers[OpenIdConstants.DPoP.HeaderName] = "proof-jwt";
        MockValidator
            .Setup(x =>
                x.ValidateAsync(
                    MockContext.Object,
                    It.IsAny<DPoPProofValidationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("jkt-thumbprint")
            .Verifiable();

        var binding = await Service.EvaluateTokenRequestAsync(
            MockContext.Object,
            MockClient.Object,
            CancellationToken.None
        );

        Assert.True(binding.IsSenderConstrained);
        Assert.Equal("jkt-thumbprint", binding.Confirmation.JwkThumbprint);
    }

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenSenderConstraintRequiredAndCertificateBound_ReturnsBinding()
    {
        SetupSettings(requireDPoP: false, certificateBound: true);
        SetupSenderConstraintRequired();
        using var certificate = CreateSelfSignedCertificate();
        MockCertificateResolver
            .Setup(x =>
                x.ResolveCertificateAsync(MockContext.Object, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new PresentedClientCertificate(certificate))
            .Verifiable();

        var binding = await Service.EvaluateTokenRequestAsync(
            MockContext.Object,
            MockClient.Object,
            CancellationToken.None
        );

        Assert.True(binding.IsSenderConstrained);
        Assert.NotNull(binding.Confirmation.X509CertificateSha256Thumbprint);
    }

    #endregion

    #region ValidateResourceAccessAsync Tests

    private void SetupTenantSettings()
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettingsProvider = CreateLooseMock<IReadOnlySettingCollectionProvider>();
        MockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.SettingsProvider).Returns(mockSettingsProvider.Object);
        mockSettingsProvider.SetupGet(x => x.Collection).Returns(MockSettings.Object);
    }

    [Fact]
    public async Task ValidateResourceAccessAsync_WhenUnbound_ReturnsNull()
    {
        var error = await Service.ValidateResourceAccessAsync(
            MockContext.Object,
            "access-token",
            default,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateResourceAccessAsync_WhenDPoPSchemeMissing_ReturnsError()
    {
        var confirmation = new TokenConfirmation { JwkThumbprint = "jkt-1" };

        var error = await Service.ValidateResourceAccessAsync(
            MockContext.Object,
            "access-token",
            confirmation,
            CancellationToken.None
        );

        Assert.NotNull(error);
    }

    [Fact]
    public async Task ValidateResourceAccessAsync_WhenDPoPJktMatches_ReturnsNull()
    {
        SetupTenantSettings();
        HttpContext.Request.Headers.Authorization = "DPoP access-token";
        HttpContext.Request.Headers[OpenIdConstants.DPoP.HeaderName] = "proof-jwt";
        MockValidator
            .Setup(x =>
                x.ValidateAsync(
                    MockContext.Object,
                    It.Is<DPoPProofValidationRequest>(r =>
                        r.ProofJwt == "proof-jwt" && r.AccessToken == "access-token"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("jkt-1")
            .Verifiable();

        var confirmation = new TokenConfirmation { JwkThumbprint = "jkt-1" };

        var error = await Service.ValidateResourceAccessAsync(
            MockContext.Object,
            "access-token",
            confirmation,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateResourceAccessAsync_WhenDPoPJktMismatch_ReturnsError()
    {
        SetupTenantSettings();
        HttpContext.Request.Headers.Authorization = "DPoP access-token";
        HttpContext.Request.Headers[OpenIdConstants.DPoP.HeaderName] = "proof-jwt";
        MockValidator
            .Setup(x =>
                x.ValidateAsync(
                    MockContext.Object,
                    It.IsAny<DPoPProofValidationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("jkt-other")
            .Verifiable();

        var confirmation = new TokenConfirmation { JwkThumbprint = "jkt-1" };

        var error = await Service.ValidateResourceAccessAsync(
            MockContext.Object,
            "access-token",
            confirmation,
            CancellationToken.None
        );

        Assert.NotNull(error);
    }

    [Fact]
    public async Task ValidateResourceAccessAsync_WhenCertificateMatches_ReturnsNull()
    {
        using var certificate = CreateSelfSignedCertificate();
        var thumbprint = Base64Url.Encode(SHA256.HashData(certificate.RawData));
        MockCertificateResolver
            .Setup(x =>
                x.ResolveCertificateAsync(MockContext.Object, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new PresentedClientCertificate(certificate))
            .Verifiable();

        var confirmation = new TokenConfirmation { X509CertificateSha256Thumbprint = thumbprint };

        var error = await Service.ValidateResourceAccessAsync(
            MockContext.Object,
            "access-token",
            confirmation,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateResourceAccessAsync_WhenCertificateMismatch_ReturnsError()
    {
        using var certificate = CreateSelfSignedCertificate();
        MockCertificateResolver
            .Setup(x =>
                x.ResolveCertificateAsync(MockContext.Object, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new PresentedClientCertificate(certificate))
            .Verifiable();

        var confirmation = new TokenConfirmation
        {
            X509CertificateSha256Thumbprint = "different-thumbprint",
        };

        var error = await Service.ValidateResourceAccessAsync(
            MockContext.Object,
            "access-token",
            confirmation,
            CancellationToken.None
        );

        Assert.NotNull(error);
    }

    #endregion

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test-client",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );
    }
}
