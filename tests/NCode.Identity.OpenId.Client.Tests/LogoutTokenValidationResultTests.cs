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

using System.Text.Json;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests;

public class LogoutTokenValidationResultTests
{
    private static JsonElement CreatePayload() => JsonElement.Parse("{}");

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenValid_ExposesValues()
    {
        var payload = CreatePayload();
        var expiresAt = DateTimeOffset.UnixEpoch.AddHours(1);

        var result = new LogoutTokenValidationResult(
            payload,
            "https://op.example.test",
            "jti-1",
            expiresAt,
            "subject-1",
            "session-1"
        );

        Assert.True(result.IsValid);
        Assert.Null(result.FailureCode);
        Assert.Null(result.FailureReason);
        Assert.Equal(payload.ToString(), result.Payload.ToString());
        Assert.Equal("https://op.example.test", result.Issuer);
        Assert.Equal("jti-1", result.TokenId);
        Assert.Equal(expiresAt, result.ExpiresAt);
        Assert.Equal("subject-1", result.Subject);
        Assert.Equal("session-1", result.SessionId);
    }

    [Fact]
    public void Constructor_WhenOnlySessionId_IsValid()
    {
        var result = new LogoutTokenValidationResult(
            CreatePayload(),
            "https://op.example.test",
            "jti-1",
            DateTimeOffset.UnixEpoch,
            null,
            "session-1"
        );

        Assert.True(result.IsValid);
        Assert.Null(result.Subject);
        Assert.Equal("session-1", result.SessionId);
    }

    [Fact]
    public void Constructor_WhenIssuerIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LogoutTokenValidationResult(
                CreatePayload(),
                "",
                "jti-1",
                DateTimeOffset.UnixEpoch,
                "subject-1",
                null
            )
        );
    }

    [Fact]
    public void Constructor_WhenTokenIdIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LogoutTokenValidationResult(
                CreatePayload(),
                "https://op.example.test",
                "",
                DateTimeOffset.UnixEpoch,
                "subject-1",
                null
            )
        );
    }

    [Fact]
    public void Constructor_WhenNeitherSubjectNorSessionId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LogoutTokenValidationResult(
                CreatePayload(),
                "https://op.example.test",
                "jti-1",
                DateTimeOffset.UnixEpoch,
                null,
                ""
            )
        );
    }

    [Fact]
    public void Constructor_WhenFailureReason_IsInvalid()
    {
        var result = new LogoutTokenValidationResult(
            TokenValidationFailureCodes.InvalidSignature,
            "bad signature"
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidSignature, result.FailureCode);
        Assert.Equal("bad signature", result.FailureReason);
        Assert.Null(result.TokenId);
        Assert.Null(result.Issuer);
    }

    [Fact]
    public void Constructor_WhenFailureCodeIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LogoutTokenValidationResult("", "bad signature")
        );
    }

    [Fact]
    public void Constructor_WhenFailureReasonIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LogoutTokenValidationResult(TokenValidationFailureCodes.InvalidSignature, "")
        );
    }

    #endregion
}
