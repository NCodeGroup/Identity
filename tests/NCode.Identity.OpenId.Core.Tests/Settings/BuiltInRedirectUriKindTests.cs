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
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class BuiltInRedirectUriKindTests : BaseTests
{
    #region Matches Tests

    [Theory]
    [InlineData("https://client.example/cb", true)]
    [InlineData("http://client.example/cb", false)]
    [InlineData("myapp://callback", false)]
    public void HttpsRedirectUriKind_Matches_OnlyAnHttpsUri(string redirectUri, bool expected)
    {
        var kind = new HttpsRedirectUriKind();

        var actual = kind.Matches(new Uri(redirectUri));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("http://127.0.0.1/cb", true)]
    [InlineData("http://127.0.0.1:51234/cb", true)]
    [InlineData("http://[::1]:8080/cb", true)]
    [InlineData("http://localhost/cb", false)]
    [InlineData("https://127.0.0.1/cb", false)]
    [InlineData("http://127.0.0.1.example.com/cb", false)]
    [InlineData("http://127.0.0.2/cb", false)]
    public void LoopbackRedirectUriKind_Matches_OnlyAnHttpLoopbackIpLiteral(
        string redirectUri,
        bool expected
    )
    {
        var kind = new LoopbackRedirectUriKind();

        var actual = kind.Matches(new Uri(redirectUri));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("http://client.example/cb", true)]
    [InlineData("http://localhost:3000/cb", true)]
    [InlineData("http://127.0.0.1/cb", false)]
    [InlineData("https://client.example/cb", false)]
    public void HttpRedirectUriKind_Matches_AnHttpUriThatIsNotLoopback(
        string redirectUri,
        bool expected
    )
    {
        var kind = new HttpRedirectUriKind();

        var actual = kind.Matches(new Uri(redirectUri));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("myapp://callback", true)]
    [InlineData("com.example.app:/callback", true)]
    [InlineData("https://client.example/cb", false)]
    [InlineData("http://client.example/cb", false)]
    public void PrivateUseRedirectUriKind_Matches_AnyOtherScheme(string redirectUri, bool expected)
    {
        var kind = new PrivateUseRedirectUriKind();

        var actual = kind.Matches(new Uri(redirectUri));

        Assert.Equal(expected, actual);
    }

    #endregion

    #region KindName Tests

    [Fact]
    public void KindName_IsTheRegisteredName()
    {
        Assert.Equal(OpenIdConstants.RedirectUriKinds.Https, new HttpsRedirectUriKind().KindName);
        Assert.Equal(
            OpenIdConstants.RedirectUriKinds.Loopback,
            new LoopbackRedirectUriKind().KindName
        );
        Assert.Equal(OpenIdConstants.RedirectUriKinds.Http, new HttpRedirectUriKind().KindName);
        Assert.Equal(
            OpenIdConstants.RedirectUriKinds.PrivateUse,
            new PrivateUseRedirectUriKind().KindName
        );
    }

    #endregion
}
