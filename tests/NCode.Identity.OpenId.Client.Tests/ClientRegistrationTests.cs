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

using Xunit;

namespace NCode.Identity.OpenId.Client.Tests;

public class ClientRegistrationTests
{
    private static ClientRegistration CreateRegistration(IReadOnlyList<string>? algorithms) =>
        new(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            IdTokenSigningAlgorithms = algorithms,
        };

    private static ClientRegistration CreateKeyManagementRegistration(
        IReadOnlyList<string>? algorithms
    ) =>
        new(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            IdTokenKeyManagementAlgorithms = algorithms,
        };

    private static ClientRegistration CreateContentEncryptionRegistration(
        IReadOnlyList<string>? algorithms
    ) =>
        new(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            IdTokenContentEncryptionAlgorithms = algorithms,
        };

    #region IdTokenSigningAlgorithms Tests

    [Fact]
    public void IdTokenSigningAlgorithms_WhenNotSet_ThenNull()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );

        Assert.Null(registration.IdTokenSigningAlgorithms);
    }

    [Fact]
    public void IdTokenSigningAlgorithms_WhenValid_ThenRoundTripsInOrder()
    {
        var registration = CreateRegistration(["PS256", "ES256"]);

        Assert.Equal(["PS256", "ES256"], registration.IdTokenSigningAlgorithms);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void IdTokenSigningAlgorithms_WhenBlankEntry_ThenThrows(string entry)
    {
        Assert.Throws<ArgumentException>(() => CreateRegistration(["PS256", entry]));
    }

    [Fact]
    public void IdTokenSigningAlgorithms_WhenNullEntry_ThenThrows()
    {
        Assert.Throws<ArgumentException>(() => CreateRegistration(["PS256", null!]));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("NONE")]
    public void IdTokenSigningAlgorithms_WhenNone_ThenThrows(string entry)
    {
        Assert.Throws<ArgumentException>(() => CreateRegistration([entry]));
    }

    [Fact]
    public void IdTokenSigningAlgorithms_WhenDuplicate_ThenThrows()
    {
        Assert.Throws<ArgumentException>(() => CreateRegistration(["PS256", "ES256", "PS256"]));
    }

    #endregion

    #region IdTokenKeyManagementAlgorithms Tests

    [Fact]
    public void IdTokenKeyManagementAlgorithms_WhenNotSet_ThenNull()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );

        Assert.Null(registration.IdTokenKeyManagementAlgorithms);
    }

    [Fact]
    public void IdTokenKeyManagementAlgorithms_WhenValid_ThenRoundTripsInOrder()
    {
        var registration = CreateKeyManagementRegistration(["RSA-OAEP-256", "ECDH-ES"]);

        Assert.Equal(["RSA-OAEP-256", "ECDH-ES"], registration.IdTokenKeyManagementAlgorithms);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void IdTokenKeyManagementAlgorithms_WhenBlankEntry_ThenThrows(string entry)
    {
        Assert.Throws<ArgumentException>(() =>
            CreateKeyManagementRegistration(["RSA-OAEP-256", entry])
        );
    }

    [Fact]
    public void IdTokenKeyManagementAlgorithms_WhenDuplicate_ThenThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateKeyManagementRegistration(["RSA-OAEP-256", "ECDH-ES", "RSA-OAEP-256"])
        );
    }

    #endregion

    #region IdTokenContentEncryptionAlgorithms Tests

    [Fact]
    public void IdTokenContentEncryptionAlgorithms_WhenNotSet_ThenNull()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );

        Assert.Null(registration.IdTokenContentEncryptionAlgorithms);
    }

    [Fact]
    public void IdTokenContentEncryptionAlgorithms_WhenValid_ThenRoundTripsInOrder()
    {
        var registration = CreateContentEncryptionRegistration(["A256GCM", "A128CBC-HS256"]);

        Assert.Equal(["A256GCM", "A128CBC-HS256"], registration.IdTokenContentEncryptionAlgorithms);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void IdTokenContentEncryptionAlgorithms_WhenBlankEntry_ThenThrows(string entry)
    {
        Assert.Throws<ArgumentException>(() =>
            CreateContentEncryptionRegistration(["A256GCM", entry])
        );
    }

    [Fact]
    public void IdTokenContentEncryptionAlgorithms_WhenDuplicate_ThenThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateContentEncryptionRegistration(["A256GCM", "A128CBC-HS256", "A256GCM"])
        );
    }

    #endregion

    #region BackchannelLogoutSessionRequired Tests

    [Fact]
    public void BackchannelLogoutSessionRequired_WhenNotSet_ThenFalse()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );

        Assert.False(registration.BackchannelLogoutSessionRequired);
    }

    [Fact]
    public void BackchannelLogoutSessionRequired_WhenSet_ThenTrue()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            BackchannelLogoutSessionRequired = true,
        };

        Assert.True(registration.BackchannelLogoutSessionRequired);
    }

    #endregion

    #region RequirePushedAuthorizationRequests Tests

    [Fact]
    public void RequirePushedAuthorizationRequests_WhenNotSet_ThenFalse()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );

        Assert.False(registration.RequirePushedAuthorizationRequests);
    }

    [Fact]
    public void RequirePushedAuthorizationRequests_WhenSet_ThenTrue()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            RequirePushedAuthorizationRequests = true,
        };

        Assert.True(registration.RequirePushedAuthorizationRequests);
    }

    #endregion

    #region DPoPKeyRing Tests

    [Fact]
    public void DPoPKeyRing_WhenNotSet_ThenNull()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );

        Assert.Null(registration.DPoPKeyRing);
    }

    [Fact]
    public void DPoPKeyRing_WhenSet_ThenValue()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            DPoPKeyRing = "dpop",
        };

        Assert.Equal("dpop", registration.DPoPKeyRing);
    }

    #endregion

    #region DPoPBoundAccessTokens Tests

    [Fact]
    public void DPoPBoundAccessTokens_WhenNotSet_ThenFalse()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );

        Assert.False(registration.DPoPBoundAccessTokens);
    }

    [Fact]
    public void DPoPBoundAccessTokens_WhenSet_ThenTrue()
    {
        var registration = new ClientRegistration(
            "https://issuer.example",
            "client",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            DPoPBoundAccessTokens = true,
        };

        Assert.True(registration.DPoPBoundAccessTokens);
    }

    #endregion
}
