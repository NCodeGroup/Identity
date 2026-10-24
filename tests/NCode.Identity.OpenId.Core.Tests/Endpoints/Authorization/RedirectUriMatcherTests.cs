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

using NCode.Identity.OpenId.Authentication.Endpoints.Authorization;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization;

public class RedirectUriMatcherTests
{
    #region MatchesExactly Tests

    [Theory]
    [InlineData("https://client.example/callback", "https://client.example/callback", true)]
    [InlineData("https://client.example/callback/", "https://client.example/callback", false)]
    [InlineData("https://CLIENT.example/callback", "https://client.example/callback", false)]
    [InlineData("https://client.example/callback?x=1", "https://client.example/callback", false)]
    [InlineData(
        "https://client.example/callback?tenant=a",
        "https://client.example/callback?tenant=a",
        true
    )]
    [InlineData(
        "https://client.example@evil.example/callback",
        "https://client.example/callback",
        false
    )]
    [InlineData("https://client.example./callback", "https://client.example/callback", false)]
    [InlineData("https://client.example/%63allback", "https://client.example/callback", false)]
    [InlineData("https://client.example/callback#x", "https://client.example/callback", false)]
    [InlineData("HTTPS://client.example/callback", "https://client.example/callback", false)]
    [InlineData(" https://client.example/callback", "https://client.example/callback", false)]
    public void MatchesExactly_ComparesTheStringsOrdinally(
        string requested,
        string registered,
        bool expected
    )
    {
        var result = RedirectUriMatcher.MatchesExactly(requested, registered);

        Assert.Equal(expected, result);
    }

    #endregion

    #region MatchesLoopbackWithAnyPort Tests

    [Theory]
    [InlineData("http://127.0.0.1:51234/callback", "http://127.0.0.1/callback", true)]
    [InlineData("http://127.0.0.1:51234/callback", "http://127.0.0.1:8080/callback", true)]
    [InlineData("http://[::1]:51234/callback", "http://[::1]/callback", true)]
    [InlineData("http://127.0.0.1:51234", "http://127.0.0.1", true)]
    [InlineData("http://127.0.0.1:51234/other", "http://127.0.0.1/callback", false)]
    [InlineData("http://[::1]:51234/callback", "http://127.0.0.1/callback", false)]
    [InlineData("http://localhost:51234/callback", "http://localhost/callback", false)]
    [InlineData("https://127.0.0.1:51234/callback", "https://127.0.0.1/callback", false)]
    [InlineData(
        "http://127.0.0.1.evil.example:1/callback",
        "http://127.0.0.1.evil.example/callback",
        false
    )]
    [InlineData("http://127.0.0.1:51234/callback?x=1", "http://127.0.0.1/callback", false)]
    [InlineData("http://127.0.0.1:/callback", "http://127.0.0.1/callback", false)]
    [InlineData("http://127.0.0.1:80@evil.example/callback", "http://127.0.0.1/callback", false)]
    [InlineData("http://127.0.0.1@evil.example/callback", "http://127.0.0.1/callback", false)]
    [InlineData("http://127.0.0.1./callback", "http://127.0.0.1/callback", false)]
    [InlineData("HTTP://127.0.0.1:1234/callback", "http://127.0.0.1/callback", false)]
    [InlineData("http://127.0.0.1:1234/callback#x", "http://127.0.0.1/callback", false)]
    [InlineData("http://127.0.0.1:1234/%63allback", "http://127.0.0.1/callback", false)]
    [InlineData("http://127.0.0.1:1234//evil.example/callback", "http://127.0.0.1/callback", false)]
    [InlineData("http://127.0.0.1:12ab/callback", "http://127.0.0.1/callback", false)]
    [InlineData("http://127.0.0.1:\u0661\u0662\u0663/callback", "http://127.0.0.1/callback", false)]
    [InlineData("http://[::1%25eth0]:1234/callback", "http://[::1]/callback", false)]
    [InlineData("http://127.0.0.1:51234/callback?x=1", "http://127.0.0.1/callback?x=1", true)]
    [InlineData("http://127.0.0.1:9090", "http://127.0.0.1:8080", true)]
    public void MatchesLoopbackWithAnyPort_IgnoresOnlyThePortOfALoopbackIpUri(
        string requested,
        string registered,
        bool expected
    )
    {
        var result = RedirectUriMatcher.MatchesLoopbackWithAnyPort(requested, registered);

        Assert.Equal(expected, result);
    }

    #endregion
}
