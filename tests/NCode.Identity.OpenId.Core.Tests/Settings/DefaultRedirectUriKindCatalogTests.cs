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

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class DefaultRedirectUriKindCatalogTests : BaseTests
{
    private static DefaultRedirectUriKindCatalog CreateCatalog() =>
        new([
            new HttpsRedirectUriKind(),
            new LoopbackRedirectUriKind(),
            new HttpRedirectUriKind(),
            new PrivateUseRedirectUriKind(),
        ]);

    private IReadOnlySettingCollection CreateSettings(IReadOnlyCollection<string>? kinds)
    {
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.RedirectUriKindsSupported, out kinds))
            .Returns(kinds is not null);
        return mockSettings.Object;
    }

    #region IsPermitted Tests

    [Theory]
    [InlineData("https://client.example/cb")]
    [InlineData("http://client.example/cb")]
    [InlineData("http://127.0.0.1:5000/cb")]
    [InlineData("com.example.app:/callback")]
    public void IsPermitted_WhenNoKindIsListed_PermitsAUriOfAnyRegisteredKind(string redirectUri)
    {
        var catalog = CreateCatalog();
        var settings = CreateSettings(null);

        var actual = catalog.IsPermitted(settings, redirectUri);

        Assert.True(actual);
    }

    [Theory]
    [InlineData("myapp://callback")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("ws://client.example/cb")]
    [InlineData("file:///etc/passwd")]
    public void IsPermitted_WhenNoKindIsListed_RefusesAUriOfNoRegisteredKind(string redirectUri)
    {
        var catalog = CreateCatalog();
        var settings = CreateSettings(null);

        var actual = catalog.IsPermitted(settings, redirectUri);

        Assert.False(actual);
    }

    [Fact]
    public void IsPermitted_WhenNoKindIsListed_RefusesAValueThatIsNotAnAbsoluteUri()
    {
        var catalog = CreateCatalog();
        var settings = CreateSettings(null);

        var actual = catalog.IsPermitted(settings, "callback");

        Assert.False(actual);
    }

    [Theory]
    [InlineData("https://client.example/cb", true)]
    [InlineData("http://client.example/cb", false)]
    [InlineData("http://127.0.0.1:5000/cb", false)]
    [InlineData("myapp://callback", false)]
    public void IsPermitted_WhenOnlyHttpsIsListed_PermitsOnlyHttps(
        string redirectUri,
        bool expected
    )
    {
        var catalog = CreateCatalog();
        var settings = CreateSettings([OpenIdConstants.RedirectUriKinds.Https]);

        var actual = catalog.IsPermitted(settings, redirectUri);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("https://client.example/cb", true)]
    [InlineData("http://127.0.0.1:5000/cb", true)]
    [InlineData("http://[::1]/cb", true)]
    [InlineData("myapp://callback", false)]
    [InlineData("com.example.app:/callback", true)]
    [InlineData("http://localhost:5000/cb", false)]
    [InlineData("http://client.example/cb", false)]
    public void IsPermitted_UnderTheFapi2Kinds_RefusesHttpAndSchemesOfNoKind(
        string redirectUri,
        bool expected
    )
    {
        var catalog = CreateCatalog();
        var settings = CreateSettings([
            OpenIdConstants.RedirectUriKinds.Https,
            OpenIdConstants.RedirectUriKinds.Loopback,
            OpenIdConstants.RedirectUriKinds.PrivateUse,
        ]);

        var actual = catalog.IsPermitted(settings, redirectUri);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsPermitted_WhenTheListedKindIsNotRegistered_RefusesTheRedirectUri()
    {
        var catalog = CreateCatalog();
        var settings = CreateSettings(["claimed_https"]);

        var actual = catalog.IsPermitted(settings, "https://client.example/cb");

        Assert.False(actual);
    }

    [Fact]
    public void IsPermitted_WhenTheValueIsNotAnAbsoluteUri_RefusesIt()
    {
        var catalog = CreateCatalog();
        var settings = CreateSettings([OpenIdConstants.RedirectUriKinds.Https]);

        var actual = catalog.IsPermitted(settings, "callback");

        Assert.False(actual);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenTwoKindsShareAName_Throws()
    {
        var mockFirst = CreateStrictMock<IRedirectUriKind>();
        mockFirst
            .Setup(x => x.KindName)
            .Returns(OpenIdConstants.RedirectUriKinds.Https)
            .Verifiable();

        var mockSecond = CreateStrictMock<IRedirectUriKind>();
        mockSecond
            .Setup(x => x.KindName)
            .Returns(OpenIdConstants.RedirectUriKinds.Https)
            .Verifiable();

        IEnumerable<IRedirectUriKind> kinds = [mockFirst.Object, mockSecond.Object];

        Assert.Throws<InvalidOperationException>(() => new DefaultRedirectUriKindCatalog(kinds));
    }

    #endregion
}
