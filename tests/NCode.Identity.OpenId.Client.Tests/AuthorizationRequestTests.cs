// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Client.Tests;

public class AuthorizationRequestTests
{
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenValid_ExposesValues()
    {
        var request = new AuthorizationRequest("https://app.example/callback", "state-1", Verifier);

        Assert.Equal("https://app.example/callback", request.RedirectUri);
        Assert.Equal("state-1", request.State);
        Assert.Equal(Verifier, request.CodeVerifier);
    }

    [Fact]
    public void Constructor_WhenVerifierIsTooShort_Throws()
    {
        var verifier = new string('a', 42);

        Assert.Throws<ArgumentException>(() =>
            new AuthorizationRequest("https://app.example/callback", "state-1", verifier)
        );
    }

    [Fact]
    public void Constructor_WhenVerifierIsTooLong_Throws()
    {
        var verifier = new string('a', 129);

        Assert.Throws<ArgumentException>(() =>
            new AuthorizationRequest("https://app.example/callback", "state-1", verifier)
        );
    }

    [Fact]
    public void Constructor_WhenVerifierHasInvalidCharacter_Throws()
    {
        var verifier = new string('a', 42) + "+";

        Assert.Throws<ArgumentException>(() =>
            new AuthorizationRequest("https://app.example/callback", "state-1", verifier)
        );
    }

    [Fact]
    public void Constructor_WhenStateIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AuthorizationRequest("https://app.example/callback", "", Verifier)
        );
    }

    [Fact]
    public void Constructor_WhenRedirectUriIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AuthorizationRequest("", "state-1", Verifier));
    }

    #endregion

    #region AdditionalParameters Tests

    [Fact]
    public void AdditionalParameters_WhenNotSet_ThenNull()
    {
        var request = new AuthorizationRequest("https://app.example/callback", "state-1", Verifier);

        Assert.Null(request.AdditionalParameters);
    }

    [Fact]
    public void AdditionalParameters_WhenSet_CopiesValues()
    {
        var source = new Dictionary<string, string> { ["prompt"] = "login" };

        var request = new AuthorizationRequest("https://app.example/callback", "state-1", Verifier)
        {
            AdditionalParameters = source,
        };
        source["max_age"] = "60";

        Assert.NotNull(request.AdditionalParameters);
        var parameter = Assert.Single(request.AdditionalParameters);
        Assert.Equal("prompt", parameter.Key);
        Assert.Equal("login", parameter.Value);
    }

    [Theory]
    [InlineData("client_id")]
    [InlineData("request_uri")]
    [InlineData("code_challenge_method")]
    [InlineData("response_type")]
    [InlineData("client_assertion")]
    [InlineData("dpop_jkt")]
    public void AdditionalParameters_WhenReserved_Throws(string name)
    {
        var source = new Dictionary<string, string> { [name] = "value" };

        Assert.Throws<ArgumentException>(() =>
            new AuthorizationRequest("https://app.example/callback", "state-1", Verifier)
            {
                AdditionalParameters = source,
            }
        );
    }

    [Fact]
    public void AdditionalParameters_WhenNameIsEmpty_Throws()
    {
        var source = new Dictionary<string, string> { [""] = "value" };

        Assert.Throws<ArgumentException>(() =>
            new AuthorizationRequest("https://app.example/callback", "state-1", Verifier)
            {
                AdditionalParameters = source,
            }
        );
    }

    #endregion
}
