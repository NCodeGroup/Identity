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

using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffTokenCacheTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(30);

    private static BffStoredAccessToken CreateToken(
        string value,
        DateTimeOffset expiresAt,
        string? resource = null,
        string? scope = null
    ) =>
        new()
        {
            Requirement = BffTokenRequirementNames.User,
            Resource = resource,
            Scope = scope,
            Value = value,
            TokenType = "Bearer",
            ExpiresAt = expiresAt,
        };

    #region TryGetFresh Tests

    [Fact]
    public void TryGetFresh_FreshMatch_ReturnsTheToken()
    {
        var payload = new BffSessionPayload
        {
            AccessTokens = [CreateToken("a", Now.AddMinutes(5))],
        };

        var found = BffTokenCache.TryGetFresh(
            payload,
            BffTokenRequirementNames.User,
            null,
            null,
            Now,
            Margin,
            out var token
        );

        Assert.True(found);
        Assert.Equal("a", token?.Value);
    }

    [Fact]
    public void TryGetFresh_StaleMatch_ReturnsFalse()
    {
        var payload = new BffSessionPayload
        {
            AccessTokens = [CreateToken("a", Now.AddSeconds(30))],
        };

        var found = BffTokenCache.TryGetFresh(
            payload,
            BffTokenRequirementNames.User,
            null,
            null,
            Now,
            Margin,
            out var token
        );

        Assert.False(found);
        Assert.Null(token);
    }

    [Fact]
    public void TryGetFresh_DifferentResource_ReturnsFalse()
    {
        var payload = new BffSessionPayload
        {
            AccessTokens = [CreateToken("a", Now.AddMinutes(5), "https://api")],
        };

        var found = BffTokenCache.TryGetFresh(
            payload,
            BffTokenRequirementNames.User,
            null,
            null,
            Now,
            Margin,
            out _
        );

        Assert.False(found);
    }

    #endregion

    #region WithToken Tests

    [Fact]
    public void WithToken_MatchingEntry_ReplacesIt()
    {
        var payload = new BffSessionPayload
        {
            AccessTokens = [CreateToken("old", Now.AddMinutes(1))],
        };

        var result = BffTokenCache.WithToken(payload, CreateToken("new", Now.AddMinutes(5)));

        var single = Assert.Single(result.AccessTokens);
        Assert.Equal("new", single.Value);
    }

    [Fact]
    public void WithToken_NewKey_Appends()
    {
        var payload = new BffSessionPayload
        {
            AccessTokens = [CreateToken("a", Now.AddMinutes(1))],
        };

        var result = BffTokenCache.WithToken(
            payload,
            CreateToken("b", Now.AddMinutes(5), "https://api")
        );

        Assert.Equal(2, result.AccessTokens.Count);
    }

    [Fact]
    public void WithToken_OverTheCap_DropsTheEarliestExpiry()
    {
        var tokens = new List<BffStoredAccessToken>();
        for (var i = 0; i < BffTokenCache.MaxEntries; i++)
        {
            tokens.Add(CreateToken($"t{i}", Now.AddMinutes(10 + i), $"https://api/{i}"));
        }

        var payload = new BffSessionPayload { AccessTokens = tokens };

        var result = BffTokenCache.WithToken(
            payload,
            CreateToken("extra", Now.AddMinutes(100), "https://api/extra")
        );

        Assert.Equal(BffTokenCache.MaxEntries, result.AccessTokens.Count);
        Assert.DoesNotContain(result.AccessTokens, x => x.Value == "t0");
        Assert.Contains(result.AccessTokens, x => x.Value == "extra");
    }

    #endregion
}
