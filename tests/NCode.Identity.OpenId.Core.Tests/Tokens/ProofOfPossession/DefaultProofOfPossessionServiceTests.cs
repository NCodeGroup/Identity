// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Encoders;
using NCode.Identity.AspNetCore;
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
    private Mock<OpenIdTenant> MockTenant { get; }
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
        MockTenant = CreateStrictMock<OpenIdTenant>();

        HttpContext = new DefaultHttpContext();
        HttpContext.Request.Scheme = "https";
        HttpContext.Request.Host = new HostString("op.test");
        HttpContext.Request.Path = "/token";
        HttpContext.Request.Method = "POST";

        Service = new DefaultProofOfPossessionService(
            MockValidator.Object,
            MockCertificateResolver.Object,
            MockSecurityProfileCatalog.Object
        );
    }

    #region Scaffolding

    // A request that carries or demands a binding reads the HTTP request and the error factory.
    private void SetupRequestContext()
    {
        MockContext.SetupGet(x => x.Http).Returns(HttpContext).Verifiable();

        // A value provider: only the refusing paths create an error, so its setup is left unverified and the paths that
        // succeed don't fail verification. The tests assert the refusal itself.
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }

    // A token request reads the client's binding settings.
    private void SetupSettings(bool requireDPoP, bool certificateBound)
    {
        SetupRequestContext();
        MockClient.SetupGet(x => x.Settings).Returns(MockSettings.Object).Verifiable();
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RequireDPoPBoundAccessTokens))
            .Returns(requireDPoP);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RequireTlsClientCertificateBoundAccessTokens))
            .Returns(certificateBound);
    }

    private void SetupTenant(
        string? mtlsEndpointAliasAuthority = null,
        bool comparesTheAlias = false
    )
    {
        MockContext.SetupGet(x => x.Tenant).Returns(MockTenant.Object).Verifiable();

        // When the request reaches the mTLS alias, the htu is compared with the alias URL and the base address is never
        // read, so a strict BaseAddress setup would go unconsumed.
        if (comparesTheAlias)
        {
            MockTenant
                .SetupGet(x => x.MtlsEndpointAliasAuthority)
                .Returns(mtlsEndpointAliasAuthority)
                .Verifiable();
            return;
        }

        MockTenant
            .SetupGet(x => x.BaseAddress)
            .Returns(
                new UriDescriptor
                {
                    Scheme = "https",
                    Host = new HostString("op.test"),
                    Path = PathString.Empty,
                }
            )
            .Verifiable();
        MockTenant
            .SetupGet(x => x.MtlsEndpointAliasAuthority)
            .Returns(mtlsEndpointAliasAuthority)
            .Verifiable();
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

        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, binding.TokenType);
        Assert.False(binding.IsSenderConstrained);
    }

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenTheServerSupportsBindingButTheClientDoesNotRequireIt_ReturnsUnbound()
    {
        SetupSettings(requireDPoP: false, certificateBound: false);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TlsClientCertificateBoundAccessTokens))
            .Returns(true);

        var binding = await Service.EvaluateTokenRequestAsync(
            MockContext.Object,
            MockClient.Object,
            CancellationToken.None
        );

        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, binding.TokenType);
        Assert.False(binding.IsSenderConstrained);
    }

    [Fact]
    public async Task EvaluateTokenRequestAsync_WhenDPoPProofPresent_ReturnsDPoPBinding()
    {
        SetupSettings(requireDPoP: false, certificateBound: false);
        SetupTenant();
        HttpContext.Request.Headers[OpenIdProtocol.DPoP.HeaderName] = "proof-jwt";
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

        Assert.Equal(OpenIdProtocol.TokenTypes.DPoP, binding.TokenType);
        Assert.Equal("jkt-thumbprint", binding.Confirmation.JwkThumbprint);
    }

    [Theory]
    [InlineData("attacker.test", false, "https://op.test/token")]
    [InlineData("op.test:8443", false, "https://op.test/token")]
    [InlineData("mtls.op.test:8443", false, "https://op.test/token")]
    [InlineData("mtls.op.test", true, "https://mtls.op.test/token")]
    [InlineData("MTLS.OP.TEST:443", true, "https://mtls.op.test/token")]
    public async Task EvaluateTokenRequestAsync_ComparesTheHtuWithTheTenantsPublishedUrl(
        string requestHost,
        bool comparesTheAlias,
        string expectedHttpUri
    )
    {
        SetupSettings(requireDPoP: false, certificateBound: false);
        SetupTenant(mtlsEndpointAliasAuthority: "https://mtls.op.test", comparesTheAlias);
        HttpContext.Request.Host = new HostString(requestHost);
        HttpContext.Request.Headers[OpenIdProtocol.DPoP.HeaderName] = "proof-jwt";
        MockValidator
            .Setup(x =>
                x.ValidateAsync(
                    MockContext.Object,
                    It.Is<DPoPProofValidationRequest>(r => r.HttpUri == expectedHttpUri),
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

        Assert.Equal(OpenIdProtocol.TokenTypes.DPoP, binding.TokenType);
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
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, binding.TokenType);
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
        SetupTenant();
        HttpContext.Request.Headers[OpenIdProtocol.DPoP.HeaderName] = "proof-jwt";
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
        SetupTenant();
        var mockSettingsProvider = CreateLooseMock<IReadOnlySettingCollectionProvider>();
        MockTenant
            .SetupGet(x => x.SettingsProvider)
            .Returns(mockSettingsProvider.Object)
            .Verifiable();
        mockSettingsProvider.SetupGet(x => x.Collection).Returns(MockSettings.Object).Verifiable();
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
        SetupRequestContext();
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
        SetupRequestContext();
        SetupTenantSettings();
        HttpContext.Request.Headers.Authorization = "DPoP access-token";
        HttpContext.Request.Headers[OpenIdProtocol.DPoP.HeaderName] = "proof-jwt";
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
        SetupRequestContext();
        SetupTenantSettings();
        HttpContext.Request.Headers.Authorization = "DPoP access-token";
        HttpContext.Request.Headers[OpenIdProtocol.DPoP.HeaderName] = "proof-jwt";
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
        SetupRequestContext();
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
        SetupRequestContext();
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
