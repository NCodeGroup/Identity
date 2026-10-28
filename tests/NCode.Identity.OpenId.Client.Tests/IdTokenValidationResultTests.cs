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

public class IdTokenValidationResultTests
{
    private static JsonElement CreatePayload() => JsonDocument.Parse("{}").RootElement;

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenValid_HasNoFailure()
    {
        var payload = CreatePayload();

        var result = new IdTokenValidationResult(payload);

        Assert.True(result.IsValid);
        Assert.Null(result.FailureCode);
        Assert.Null(result.FailureReason);
        Assert.Equal(payload.ToString(), result.Payload.ToString());
    }

    [Fact]
    public void Constructor_WhenFailure_ExposesCodeAndReason()
    {
        var result = new IdTokenValidationResult(
            TokenValidationFailureCodes.NonceMismatch,
            "bad nonce"
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.NonceMismatch, result.FailureCode);
        Assert.Equal("bad nonce", result.FailureReason);
    }

    [Fact]
    public void Constructor_WhenFailureCodeIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => new IdTokenValidationResult("", "bad nonce"));
    }

    [Fact]
    public void Constructor_WhenFailureReasonIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new IdTokenValidationResult(TokenValidationFailureCodes.NonceMismatch, "")
        );
    }

    #endregion
}
