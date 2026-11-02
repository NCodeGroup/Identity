// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests;

public class IdTokenValidationResultTests
{
    private static JsonElement CreatePayload() => JsonElement.Parse("{}");

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
