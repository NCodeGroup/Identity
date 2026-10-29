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

using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId;
using Xunit;

namespace NCode.Identity.Bff.Tests.Client;

public class BffClientRegistrationProviderTests : BaseTests
{
    private static BffClientRegistrationProvider CreateProvider(string mode)
    {
        var bffOptions = new BffOptions
        {
            Authority = "https://op.example.com",
            ClientId = "bff",
            BackchannelLogoutSessionRequired = false,
            ClientAuthentication = new BffClientAuthenticationOptions { KeyRing = "client-keys" },
            DPoP = new BffDPoPOptions { Mode = mode, KeyRing = "dpop-keys" },
        };
        var options = Microsoft.Extensions.Options.Options.Create(bffOptions);
        return new BffClientRegistrationProvider(options);
    }

    #region Registration Tests

    [Fact]
    public void Registration_Required_BindsDPoPAndOptions()
    {
        var provider = CreateProvider(BffDPoPModes.Required);

        var registration = provider.Registration;

        Assert.Equal("https://op.example.com", registration.Authority);
        Assert.Equal("bff", registration.ClientId);
        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            registration.AuthenticationMethod
        );
        Assert.Equal("client-keys", registration.KeyRing);
        Assert.Equal("dpop-keys", registration.DPoPKeyRing);
        Assert.True(registration.DPoPBoundAccessTokens);
        Assert.False(registration.BackchannelLogoutSessionRequired);
        Assert.Same(registration, provider.Registration);
    }

    [Fact]
    public void Registration_WhenSupported_DoesNotBindAccessTokens()
    {
        var provider = CreateProvider(BffDPoPModes.WhenSupported);

        var registration = provider.Registration;

        Assert.Equal("dpop-keys", registration.DPoPKeyRing);
        Assert.False(registration.DPoPBoundAccessTokens);
    }

    [Fact]
    public void Registration_BindsTheIdTokenAndUserInfoProtection()
    {
        var bffOptions = new BffOptions
        {
            Authority = "https://op.example.com",
            ClientId = "bff",
            IdToken = new BffIdTokenOptions
            {
                SigningAlgorithms = ["PS256"],
                KeyManagementAlgorithms = ["RSA-OAEP-256"],
                ContentEncryptionAlgorithms = ["A256GCM"],
            },
            UserInfo = new BffUserInfoOptions
            {
                Load = true,
                SigningAlgorithms = ["ES256"],
                KeyManagementAlgorithms = ["ECDH-ES"],
                ContentEncryptionAlgorithms = ["A128GCM"],
            },
        };
        var provider = new BffClientRegistrationProvider(
            Microsoft.Extensions.Options.Options.Create(bffOptions)
        );

        var registration = provider.Registration;

        Assert.Equal(["PS256"], registration.IdTokenSigningAlgorithms);
        Assert.Equal(["RSA-OAEP-256"], registration.IdTokenKeyManagementAlgorithms);
        Assert.Equal(["A256GCM"], registration.IdTokenContentEncryptionAlgorithms);
        Assert.Equal(["ES256"], registration.UserInfoSigningAlgorithms);
        Assert.Equal(["ECDH-ES"], registration.UserInfoKeyManagementAlgorithms);
        Assert.Equal(["A128GCM"], registration.UserInfoContentEncryptionAlgorithms);
    }

    #endregion
}
