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

using NCode.Identity.OpenId.Core.Tests.ClientCertificates;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class OpenIdSettingRulesTests
{
    private static bool IsValid<TValue>(SettingRule<TValue> rule, TValue value)
        where TValue : notnull => rule.TryValidate(value, out _);

    #region Protocol Vocabulary Tests

    [Theory]
    [InlineData("public", true)]
    [InlineData("pairwise", true)]
    [InlineData("pairwse", false)]
    [InlineData("", false)]
    public void SubjectType_AcceptsOnlyTheSubjectTypes(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.SubjectType(), value));
    }

    [Theory]
    [InlineData("poll", true)]
    [InlineData("ping", true)]
    [InlineData("push", true)]
    [InlineData("pull", false)]
    public void BackchannelTokenDeliveryMode_AcceptsOnlyTheCibaModes(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.BackchannelTokenDeliveryMode(), value));
    }

    [Theory]
    [InlineData("code", true)]
    [InlineData("id_token", true)]
    [InlineData("token", true)]
    [InlineData("code id_token", true)]
    [InlineData("code token", true)]
    [InlineData("id_token token", true)]
    [InlineData("code id_token token", true)]
    [InlineData("id_token code", false)]
    [InlineData("token id_token code", false)]
    [InlineData("code  id_token", false)]
    [InlineData("code code", false)]
    [InlineData("none", false)]
    [InlineData("code none", false)]
    [InlineData("", false)]
    public void ResponseType_AcceptsTheResponseTypesInCanonicalForm(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.ResponseType(), value));
    }

    [Theory]
    [InlineData("query", true)]
    [InlineData("form_post", true)]
    [InlineData("query.jwt", false)]
    public void ResponseMode_AcceptsTheImplementedModes(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.ResponseMode(), value));
    }

    [Theory]
    [InlineData("none", true)]
    [InlineData("create", true)]
    [InlineData("select-account", false)]
    public void PromptValue_AcceptsThePromptValues(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.PromptValue(), value));
    }

    [Theory]
    [InlineData("page", true)]
    [InlineData("wap", true)]
    [InlineData("mobile", false)]
    public void DisplayValue_AcceptsTheDisplayValues(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.DisplayValue(), value));
    }

    [Theory]
    [InlineData("normal", true)]
    [InlineData("aggregated", false)]
    [InlineData("distributed", false)]
    public void ClaimType_AcceptsOnlyTheImplementedClaimType(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.ClaimType(), value));
    }

    [Theory]
    [InlineData("absolute", true)]
    [InlineData("sliding", true)]
    [InlineData("none", false)]
    public void RefreshTokenExpirationPolicy_NeverAcceptsANonExpiringToken(
        string value,
        bool expected
    )
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.RefreshTokenExpirationPolicy(), value));
    }

    [Theory]
    [InlineData("none", true)]
    [InlineData("revoke_all", true)]
    [InlineData("revoke-all", false)]
    public void RefreshTokenReusePolicy_AcceptsTheReusePolicies(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.RefreshTokenReusePolicy(), value));
    }

    [Theory]
    [InlineData("authorization_code", true)]
    [InlineData("implicit", true)]
    [InlineData("hybrid", false)]
    [InlineData("client_credentials", true)]
    [InlineData("urn:ietf:params:oauth:grant-type:device_code", true)]
    [InlineData("urn:openid:params:grant-type:ciba", true)]
    [InlineData("https://grants.example.com/custom", true)]
    [InlineData("authorisation_code", false)]
    [InlineData("/relative/grant", false)]
    [InlineData("", false)]
    public void GrantType_AcceptsNamedGrantTypesAndAbsoluteUris(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.GrantType(), value));
    }

    [Theory]
    [InlineData("at+jwt", true)]
    [InlineData("application/at+jwt", true)]
    [InlineData("AT+JWT", true)]
    [InlineData("JWT", false)]
    [InlineData("", false)]
    public void AccessTokenType_AcceptsOnlyTheJwtAccessTokenType(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.AccessTokenType(), value));
    }

    [Theory]
    [InlineData("online", true)]
    [InlineData("offline", true)]
    [InlineData("none", true)]
    [InlineData("Online", false)]
    [InlineData("", false)]
    public void ClientCertificateRevocationMode_AcceptsOnlyTheNamedModes(
        string value,
        bool expected
    )
    {
        Assert.Equal(
            expected,
            IsValid(OpenIdSettingRules.ClientCertificateRevocationMode(), value)
        );
    }

    #endregion

    #region Value Shape Tests

    [Theory]
    [InlineData("email", true)]
    [InlineData("urn:example:claim", true)]
    [InlineData("", false)]
    [InlineData("given name", false)]
    public void Token_RejectsEmptyAndWhitespace(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.Token(), value));
    }

    [Fact]
    public void ClaimTypes_RejectsAnEmptyList()
    {
        Assert.False(IsValid(OpenIdSettingRules.ClaimTypes(), []));
    }

    [Fact]
    public void ClaimTypes_AcceptsAListOfTokens()
    {
        Assert.True(IsValid(OpenIdSettingRules.ClaimTypes(), ["sub", "email"]));
    }

    [Theory]
    [InlineData("https://op.example.com/policy", true)]
    [InlineData("http://localhost:5000/tos", true)]
    [InlineData("ftp://op.example.com/policy", false)]
    [InlineData("op.example.com/policy", false)]
    [InlineData(" https://op.example.com/policy", false)]
    public void WebUri_AcceptsAbsoluteHttpUris(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.WebUri(), value));
    }

    [Theory]
    [InlineData("https://client.example.com/jwks", true)]
    [InlineData("http://client.example.com/jwks", false)]
    [InlineData("/jwks", false)]
    [InlineData(@"C:\jwks.json", false)]
    public void HttpsUri_AcceptsOnlyAbsoluteHttpsUris(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.HttpsUri(), value));
    }

    [Theory]
    [InlineData("urn:example:client", true)]
    [InlineData("https://client.example.com/#section", true)]
    [InlineData("client.example.com", false)]
    public void AbsoluteUri_AcceptsAnyScheme(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.AbsoluteUri(), value));
    }

    [Theory]
    [InlineData("https://client.example.com/callback", true)]
    [InlineData("com.example.app:/callback", true)]
    [InlineData("http://127.0.0.1:8080/callback", true)]
    [InlineData("https://client.example.com/callback#fragment", false)]
    [InlineData("/callback", false)]
    public void RedirectUri_AcceptsAbsoluteUrisWithoutAFragment(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.RedirectUri(), value));
    }

    [Theory]
    [InlineData("https://client.example.com/logout", true)]
    [InlineData("http://client.example.com/logout", true)]
    [InlineData("com.example.app:/logout", false)]
    [InlineData("https://client.example.com/logout#x", false)]
    public void LogoutUri_AcceptsHttpUrisWithoutAFragment(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.LogoutUri(), value));
    }

    [Theory]
    [InlineData("keys.example.com", true)]
    [InlineData("192.0.2.10", true)]
    [InlineData("[2001:db8::1]", true)]
    [InlineData("10.0.0", false)]
    [InlineData("keys.example.com:443", false)]
    [InlineData("https://keys.example.com", false)]
    [InlineData("keys.example.com/path", false)]
    [InlineData("user@keys.example.com", false)]
    public void HostName_AcceptsOnlyAHostWrittenAsAUriReportsIt(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.HostName(), value));
    }

    [Theory]
    [InlineData("application/oauth-authz-req+jwt", true)]
    [InlineData("application/jwt", true)]
    [InlineData("application/jwt; charset=utf-8", false)]
    [InlineData("jwt", false)]
    public void MediaType_AcceptsATypeAndSubtypeWithoutParameters(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.MediaType(), value));
    }

    [Theory]
    [InlineData("CN=client, O=Example", true)]
    [InlineData("   ", false)]
    public void DistinguishedName_AcceptsAParsableName(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.DistinguishedName(), value));
    }

    [Theory]
    [InlineData("client.example.com", true)]
    [InlineData("*.example.com", true)]
    [InlineData("192.0.2.10", false)]
    [InlineData("client example", false)]
    public void DnsName_AcceptsADnsName(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.DnsName(), value));
    }

    [Theory]
    [InlineData("192.0.2.10", true)]
    [InlineData("2001:db8::1", true)]
    [InlineData("10.0.0", false)]
    [InlineData("010.000.000.001", false)]
    public void IpAddress_AcceptsOnlyTheCanonicalForm(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.IpAddress(), value));
    }

    [Theory]
    [InlineData("client@example.com", true)]
    [InlineData("Client <client@example.com>", false)]
    [InlineData("client", false)]
    public void EmailAddress_AcceptsABareAddress(string value, bool expected)
    {
        Assert.Equal(expected, IsValid(OpenIdSettingRules.EmailAddress(), value));
    }

    [Fact]
    public void CaCertificatePem_CertificateAuthority_IsValid()
    {
        using var authority = TestCertificates.CreateRootAuthority("Root");

        Assert.True(
            IsValid(OpenIdSettingRules.CaCertificatePem(), authority.ExportCertificatePem())
        );
    }

    [Fact]
    public void CaCertificatePem_EndEntityCertificate_IsInvalid()
    {
        using var authority = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(authority, "CN=client");

        Assert.False(IsValid(OpenIdSettingRules.CaCertificatePem(), leaf.ExportCertificatePem()));
    }

    [Fact]
    public void CaCertificatePem_MultipleCertificates_IsInvalid()
    {
        using var first = TestCertificates.CreateRootAuthority("First");
        using var second = TestCertificates.CreateRootAuthority("Second");
        var bundle = first.ExportCertificatePem() + "\n" + second.ExportCertificatePem();

        Assert.False(IsValid(OpenIdSettingRules.CaCertificatePem(), bundle));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a certificate")]
    [InlineData("-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----")]
    public void CaCertificatePem_NotACertificate_IsInvalid(string value)
    {
        Assert.False(IsValid(OpenIdSettingRules.CaCertificatePem(), value));
    }

    #endregion

    #region Duration Tests

    [Fact]
    public void Lifetime_RequiresAtLeastOneSecond()
    {
        var rule = OpenIdSettingRules.Lifetime();

        Assert.True(IsValid(rule, TimeSpan.FromSeconds(1)));
        Assert.False(IsValid(rule, TimeSpan.FromMilliseconds(999)));
        Assert.False(IsValid(rule, TimeSpan.FromMinutes(-5)));
    }

    [Fact]
    public void Timeout_RequiresAPositiveDuration()
    {
        var rule = OpenIdSettingRules.Timeout();

        Assert.True(IsValid(rule, TimeSpan.FromMilliseconds(500)));
        Assert.False(IsValid(rule, TimeSpan.Zero));
    }

    [Fact]
    public void NonNegativeDuration_AcceptsZero()
    {
        var rule = OpenIdSettingRules.NonNegativeDuration();

        Assert.True(IsValid(rule, TimeSpan.Zero));
        Assert.False(IsValid(rule, TimeSpan.FromTicks(-1)));
    }

    #endregion
}
