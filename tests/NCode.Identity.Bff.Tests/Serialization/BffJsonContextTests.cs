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
using NCode.Buffers;
using NCode.Identity.Bff.Serialization;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Serialization;

public class BffJsonContextTests : BaseTests
{
    #region BffSessionPayload Tests

    [Fact]
    public void Payload_RoundTrip_PreservesEveryMember()
    {
        var expiresAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var claim = new BffStoredClaim
        {
            Type = "sub",
            Value = "u1",
            ValueType = "string",
        };
        var token = new BffStoredAccessToken
        {
            Requirement = "user",
            Resource = "https://api",
            Scope = "a b",
            Value = "at",
            TokenType = "DPoP",
            ExpiresAt = expiresAt,
            DPoPJwkThumbprint = "jkt",
        };
        var payload = new BffSessionPayload
        {
            Claims = [claim],
            IdToken = "id-token",
            RefreshToken = "refresh",
            AccessTokens = [token],
            DPoPJwkThumbprint = "jkt",
            LogoutGuard = "guard",
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(
            payload,
            BffJsonContext.Default.BffSessionPayload
        );
        var result = JsonSerializer.Deserialize(json, BffJsonContext.Default.BffSessionPayload);

        Assert.NotNull(result);
        Assert.Equal("id-token", result.IdToken);
        Assert.Equal("refresh", result.RefreshToken);
        Assert.Equal("guard", result.LogoutGuard);
        Assert.Equal("jkt", result.DPoPJwkThumbprint);
        Assert.Equal(claim, Assert.Single(result.Claims));
        Assert.Equal(token, Assert.Single(result.AccessTokens));
    }

    [Fact]
    public void Payload_UnknownMember_SurvivesInExtensionData()
    {
        const string input = """{"id_token":"t","future_member":42}""";

        var payload = JsonSerializer.Deserialize(input, BffJsonContext.Default.BffSessionPayload);

        Assert.NotNull(payload);
        Assert.NotNull(payload.ExtensionData);
        Assert.Equal(42, payload.ExtensionData["future_member"].GetInt32());
        var json = JsonSerializer.SerializeToUtf8Bytes(
            payload,
            BffJsonContext.Default.BffSessionPayload
        );
        var text = SecureEncoding.UTF8.GetString(json);
        Assert.Contains("\"future_member\":42", text);
    }

    #endregion

    #region BffSessionMetadata Tests

    [Fact]
    public void Metadata_Serialize_UsesSnakeCaseNames()
    {
        var metadata = new BffSessionMetadata
        {
            Frontend = "default",
            AbsoluteExpiresAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(
            metadata,
            BffJsonContext.Default.BffSessionMetadata
        );

        var text = SecureEncoding.UTF8.GetString(json);
        Assert.Contains("\"absolute_expires_at\"", text);
        Assert.DoesNotContain("session_id", text);
    }

    #endregion
}
