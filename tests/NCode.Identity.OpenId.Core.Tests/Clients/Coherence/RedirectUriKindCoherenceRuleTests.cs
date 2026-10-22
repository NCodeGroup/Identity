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
using NCode.Identity.OpenId.Authentication.Clients.Coherence;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Coherence;

public class RedirectUriKindCoherenceRuleTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }
    private Mock<IRedirectUriKindCatalog> MockCatalog { get; }

    public RedirectUriKindCoherenceRuleTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockCatalog = CreateStrictMock<IRedirectUriKindCatalog>();
    }

    #region Helpers

    private void SetupValues(SettingKey<IReadOnlyCollection<string>> key, params string[] values)
    {
        IReadOnlyCollection<string>? output = values;
        MockSettings.Setup(x => x.TryGetValue(key, out output)).Returns(true);
    }

    private void SetupIsPermitted(string redirectUri, bool isPermitted)
    {
        MockCatalog
            .Setup(x => x.IsPermitted(MockSettings.Object, redirectUri))
            .Returns(isPermitted)
            .Verifiable();
    }

    private RedirectUriKindCoherenceRule CreateRule() => new(MockCatalog.Object);

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenEveryRedirectUriIsPermitted_ReturnsEmpty()
    {
        SetupValues(OpenIdSettingKeys.RedirectUris, "https://client.example/callback");
        SetupValues(OpenIdSettingKeys.PostLogoutRedirectUris, "https://client.example/logout");
        SetupIsPermitted("https://client.example/callback", true);
        SetupIsPermitted("https://client.example/logout", true);

        var violations = CreateRule().FindViolations(MockSettings.Object);

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenARedirectUriIsNotPermitted_ReturnsItAgainstTheKinds()
    {
        SetupValues(
            OpenIdSettingKeys.RedirectUris,
            "https://client.example/callback",
            "http://client.example/callback"
        );
        SetupIsPermitted("https://client.example/callback", true);
        SetupIsPermitted("http://client.example/callback", false);

        var violations = CreateRule().FindViolations(MockSettings.Object);

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.RedirectUris,
                "http://client.example/callback",
                OpenIdSettingNames.RedirectUriKindsSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenAPostLogoutRedirectUriIsNotPermitted_ReturnsItAgainstTheKinds()
    {
        SetupValues(OpenIdSettingKeys.PostLogoutRedirectUris, "http://client.example/logout");
        SetupIsPermitted("http://client.example/logout", false);

        var violations = CreateRule().FindViolations(MockSettings.Object);

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.PostLogoutRedirectUris,
                "http://client.example/logout",
                OpenIdSettingNames.RedirectUriKindsSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenNoRedirectUriIsRegistered_ReturnsEmpty()
    {
        SetupValues(OpenIdSettingKeys.RedirectUriKindsSupported, "web");

        var violations = CreateRule().FindViolations(MockSettings.Object);

        Assert.Empty(violations);
    }

    #endregion

    #region SettingNames Tests

    [Fact]
    public void SettingNames_ListsBothRedirectUriListsAndTheKinds()
    {
        var names = CreateRule().SettingNames;

        Assert.Equal(3, names.Count);
        Assert.Contains(OpenIdSettingNames.RedirectUris, names);
        Assert.Contains(OpenIdSettingNames.PostLogoutRedirectUris, names);
        Assert.Contains(OpenIdSettingNames.RedirectUriKindsSupported, names);
    }

    #endregion
}
