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

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class OpenIdClientRegistrationExtensionsTests : BaseTests
{
    #region Scaffolding

    private (Mock<OpenIdClient> Client, Mock<IReadOnlySettingCollection> Settings) CreateClient(
        string registeredAuthMethod
    )
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(registeredAuthMethod)
            .Verifiable();
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        return (mockClient, mockSettings);
    }

    private static void SetupCeiling(
        Mock<IReadOnlySettingCollection> mockSettings,
        IReadOnlyCollection<string>? ceiling
    )
    {
        var permittedMethods = ceiling;
        mockSettings
            .Setup(x =>
                x.TryGetValue(
                    OpenIdSettingKeys.TokenEndpointAuthMethodsSupported,
                    out permittedMethods
                )
            )
            .Returns(ceiling is not null)
            .Verifiable();
    }

    #endregion

    #region RegisteredAuthenticationMethod Property Tests

    [Fact]
    public void RegisteredAuthenticationMethod_ReturnsTheEffectiveTokenEndpointAuthMethod()
    {
        var (mockClient, _) = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt
        );

        var method = mockClient.Object.RegisteredAuthenticationMethod;

        Assert.Equal(OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt, method);
    }

    [Fact]
    public void RegisteredAuthenticationMethod_WhenTheClientIsNull_Throws()
    {
        OpenIdClient? client = null;

        Assert.Throws<ArgumentNullException>(() => client!.RegisteredAuthenticationMethod);
    }

    #endregion

    #region IsRegisteredAsConfidential Property Tests

    [Theory]
    [InlineData("client_secret_basic", true)]
    [InlineData("private_key_jwt", true)]
    [InlineData("tls_client_auth", true)]
    [InlineData("none", false)]
    public void IsRegisteredAsConfidential_FollowsTheRegisteredAuthenticationMethod(
        string registeredAuthMethod,
        bool expected
    )
    {
        var (mockClient, _) = CreateClient(registeredAuthMethod);

        var isConfidential = mockClient.Object.IsRegisteredAsConfidential;

        Assert.Equal(expected, isConfidential);
    }

    [Fact]
    public void IsRegisteredAsConfidential_WhenTheClientIsNull_Throws()
    {
        OpenIdClient? client = null;

        Assert.Throws<ArgumentNullException>(() => client!.IsRegisteredAsConfidential);
    }

    #endregion

    #region IsRegisteredAuthenticationMethodPermitted Property Tests

    [Fact]
    public void IsRegisteredAuthenticationMethodPermitted_WhenTheCeilingIsUnset_ReturnsTrue()
    {
        var (mockClient, mockSettings) = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic
        );
        SetupCeiling(mockSettings, ceiling: null);

        var permitted = mockClient.Object.IsRegisteredAuthenticationMethodPermitted;

        Assert.True(permitted);
    }

    [Fact]
    public void IsRegisteredAuthenticationMethodPermitted_WhenTheCeilingListsTheMethod_ReturnsTrue()
    {
        var (mockClient, mockSettings) = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt
        );
        SetupCeiling(
            mockSettings,
            [
                OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
                OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth,
            ]
        );

        var permitted = mockClient.Object.IsRegisteredAuthenticationMethodPermitted;

        Assert.True(permitted);
    }

    [Fact]
    public void IsRegisteredAuthenticationMethodPermitted_WhenTheCeilingExcludesTheMethod_ReturnsFalse()
    {
        var (mockClient, mockSettings) = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic
        );
        SetupCeiling(mockSettings, [OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt]);

        var permitted = mockClient.Object.IsRegisteredAuthenticationMethodPermitted;

        Assert.False(permitted);
    }

    [Fact]
    public void IsRegisteredAuthenticationMethodPermitted_WhenTheCeilingIsEmpty_ReturnsFalse()
    {
        // An empty ceiling is the intersection of disjoint ceilings: it permits nothing, never everything.
        var (mockClient, mockSettings) = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic
        );
        SetupCeiling(mockSettings, []);

        var permitted = mockClient.Object.IsRegisteredAuthenticationMethodPermitted;

        Assert.False(permitted);
    }

    #endregion

    #region MayAuthenticateWith Tests

    [Fact]
    public void MayAuthenticateWith_WhenTheMethodIsRegisteredAndPermitted_ReturnsTrue()
    {
        var (mockClient, mockSettings) = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.None
        );
        SetupCeiling(mockSettings, [OpenIdConstants.ClientAuthenticationMethods.None]);

        var mayAuthenticate = mockClient.Object.MayAuthenticateWith(
            OpenIdConstants.ClientAuthenticationMethods.None
        );

        Assert.True(mayAuthenticate);
    }

    [Fact]
    public void MayAuthenticateWith_WhenTheMethodIsNotTheRegisteredOne_ReturnsFalse()
    {
        // A confidential client never authenticates as public, nor with another confidential method.
        var (mockClient, _) = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic
        );

        var mayAuthenticate = mockClient.Object.MayAuthenticateWith(
            OpenIdConstants.ClientAuthenticationMethods.None
        );

        Assert.False(mayAuthenticate);
    }

    [Fact]
    public void MayAuthenticateWith_WhenTheCeilingExcludesTheRegisteredMethod_ReturnsFalse()
    {
        var (mockClient, mockSettings) = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost
        );
        SetupCeiling(mockSettings, [OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt]);

        var mayAuthenticate = mockClient.Object.MayAuthenticateWith(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost
        );

        Assert.False(mayAuthenticate);
    }

    #endregion
}
