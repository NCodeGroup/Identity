// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Clients.Coherence;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Coherence;

public class ClientSecretKeyCoherenceRuleTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockRegisteredSettings { get; }
    private Mock<IReadOnlySettingCollection> MockEffectiveSettings { get; }
    private Mock<IClientAuthenticationMethodCatalog> MockMethodCatalog { get; }
    private Mock<IAlgorithmCollectionProvider> MockAlgorithmProvider { get; }
    private Mock<IAlgorithmCollection> MockAlgorithms { get; }

    public ClientSecretKeyCoherenceRuleTests()
    {
        // Loose: a setting the test does not set reads as unset.
        MockRegisteredSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockEffectiveSettings = CreateStrictMock<IReadOnlySettingCollection>();
        MockMethodCatalog = CreateStrictMock<IClientAuthenticationMethodCatalog>();
        MockAlgorithmProvider = CreateStrictMock<IAlgorithmCollectionProvider>();
        MockAlgorithms = CreateStrictMock<IAlgorithmCollection>();
    }

    #region Helpers

    private ClientSecretKeyCoherenceRule CreateRule() =>
        new(MockMethodCatalog.Object, MockAlgorithmProvider.Object);

    private ClientCoherenceContext CreateContext() =>
        new()
        {
            RegisteredSettings = MockRegisteredSettings.Object,
            EffectiveSettings = MockEffectiveSettings.Object,
        };

    private void SetupRegisteredAlgorithms(params string[] algorithms)
    {
        IReadOnlyCollection<string>? output = algorithms;
        MockRegisteredSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.RequestObjectSigningAlgValuesSupported, out output)
            )
            .Returns(true)
            .Verifiable();
    }

    private void SetupMethod(string method, bool usesClientSecretAsKey)
    {
        MockEffectiveSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(method)
            .Verifiable();

        var descriptor = new ClientAuthenticationMethodDescriptor
        {
            Name = method,
            UsesClientSecretAsKey = usesClientSecretAsKey,
        };
        MockMethodCatalog.Setup(x => x.TryGet(method, out descriptor)).Returns(true).Verifiable();
    }

    private void SetupAlgorithm(string code, Type keyType)
    {
        var mockAlgorithm = CreateStrictMock<SignatureAlgorithm>();
        mockAlgorithm.SetupGet(x => x.KeyType).Returns(keyType).Verifiable();
        var algorithm = mockAlgorithm.Object;
        MockAlgorithms
            .Setup(x => x.TryGetSignatureAlgorithm(code, out algorithm))
            .Returns(true)
            .Verifiable();
    }

    private void SetupAlgorithmCollection() =>
        MockAlgorithmProvider
            .SetupGet(x => x.Collection)
            .Returns(MockAlgorithms.Object)
            .Verifiable();

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenTheClientRegistersNoRequestObjectAlgorithm_ReturnsEmpty()
    {
        // An inherited list is the tenant's, not the client's registration, so the effective method is never read.
        var violations = CreateRule().FindViolations(CreateContext());

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheMethodUsesTheSecretAsAKey_ReturnsEmpty()
    {
        SetupRegisteredAlgorithms("HS256");
        SetupMethod(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt, true);

        var violations = CreateRule().FindViolations(CreateContext());

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheMethodComparesTheSecret_ReturnsEachSymmetricAlgorithm()
    {
        SetupRegisteredAlgorithms("RS256", "HS256", "unknown");
        SetupMethod(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic, false);
        SetupAlgorithmCollection();
        SetupAlgorithm("RS256", typeof(RsaSecretKey));
        SetupAlgorithm("HS256", typeof(SymmetricSecretKey));
        SignatureAlgorithm? unknown = null;
        MockAlgorithms
            .Setup(x => x.TryGetSignatureAlgorithm("unknown", out unknown))
            .Returns(false)
            .Verifiable();

        var violations = CreateRule().FindViolations(CreateContext());

        var violation = Assert.Single(violations);
        Assert.Equal(
            OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,
            violation.SettingName
        );
        Assert.Equal("HS256", violation.Value);
        Assert.Equal(OpenIdSettingNames.TokenEndpointAuthMethod, violation.GoverningSettingName);
        Assert.Equal(
            "The 'request_object_signing_alg_values_supported' value 'HS256' is keyed from the client secret, which "
                + "the effective 'token_endpoint_auth_method' value 'client_secret_basic' stores as a one-way verifier.",
            violation.Description
        );
    }

    [Fact]
    public void FindViolations_WhenTheMethodIsNotRegistered_TreatsItAsComparingTheSecret()
    {
        SetupRegisteredAlgorithms("HS256");
        MockEffectiveSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns("custom_method")
            .Verifiable();
        ClientAuthenticationMethodDescriptor? descriptor = null;
        MockMethodCatalog
            .Setup(x => x.TryGet("custom_method", out descriptor))
            .Returns(false)
            .Verifiable();
        SetupAlgorithmCollection();
        SetupAlgorithm("HS256", typeof(SymmetricSecretKey));

        var violations = CreateRule().FindViolations(CreateContext());

        Assert.Equal("HS256", Assert.Single(violations).Value);
    }

    #endregion

    #region SettingNames Tests

    [Fact]
    public void SettingNames_NameTheMethodAndTheRequestObjectAlgorithms()
    {
        Assert.Equal(
            [
                OpenIdSettingNames.TokenEndpointAuthMethod,
                OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,
            ],
            CreateRule().SettingNames
        );
    }

    #endregion
}
