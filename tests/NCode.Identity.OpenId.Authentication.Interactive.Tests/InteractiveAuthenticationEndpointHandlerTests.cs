// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Jose;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public class InteractiveAuthenticationEndpointHandlerTests
{
    private const string Origin = "https://op.example";

    #region ResolveSafeReturnUrl Tests

    [Fact]
    public void ResolveSafeReturnUrl_WhenLocalPath_ReturnsSamePath()
    {
        var result = InteractiveAuthenticationEndpointHandler.ResolveSafeReturnUrl(
            "/connect/authorize?client_id=mgmt-spa",
            Origin
        );

        Assert.Equal("/connect/authorize?client_id=mgmt-spa", result);
    }

    [Fact]
    public void ResolveSafeReturnUrl_WhenSameOriginAbsoluteUrl_ReturnsSameUrl()
    {
        // The OP's challenge sets an absolute, same-origin continue URL as the return target.
        var returnUrl = $"{Origin}/oauth2/continue?state=abc";

        var result = InteractiveAuthenticationEndpointHandler.ResolveSafeReturnUrl(
            returnUrl,
            Origin
        );

        Assert.Equal(returnUrl, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("//evil.example")] // protocol-relative authority
    [InlineData("/\\evil.example")] // backslash-escaped authority
    [InlineData("/\t/evil.example")] // tab stripped by browsers, leaving a protocol-relative authority
    [InlineData("/\t\\evil.example")] // tab stripped by browsers, leaving a backslash-escaped authority
    [InlineData("/\r\n/evil.example")] // newline stripped by browsers, leaving a protocol-relative authority
    [InlineData("https://evil.example/path")] // cross-origin absolute url
    [InlineData("evil.example")] // no leading slash
    [InlineData("javascript:alert(1)")] // scheme without slash
    public void ResolveSafeReturnUrl_WhenNonLocalOrCrossOrigin_ReturnsRoot(string? returnUrl)
    {
        var result = InteractiveAuthenticationEndpointHandler.ResolveSafeReturnUrl(
            returnUrl,
            Origin
        );

        Assert.Equal("/", result);
    }

    #endregion

    #region BuildMeResponse Tests

    [Fact]
    public void BuildMeResponse_WhenNameClaimPresent_UsesNameAndProjectsSessionClaims()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(JoseClaimNames.Payload.Name, "Ada Lovelace"),
                    new Claim(JoseClaimNames.Payload.PreferredUsername, "ada"),
                    new Claim(JoseClaimNames.Payload.AuthTime, "1700000000"),
                    new Claim(JoseClaimNames.Payload.Amr, "pwd"),
                    new Claim(JoseClaimNames.Payload.Amr, "mfa"),
                    new Claim(JoseClaimNames.Payload.Idp, "local"),
                    new Claim(JoseClaimNames.Payload.Sid, "session-123"),
                ],
                authenticationType: "cookie"
            )
        );

        var result = InteractiveAuthenticationEndpointHandler.BuildMeResponse(
            principal,
            "subject-1"
        );

        Assert.Equal("subject-1", result.Subject);
        Assert.Equal("Ada Lovelace", result.Name);
        Assert.Equal(1700000000L, result.AuthTime);
        Assert.Equal(["pwd", "mfa"], result.Amr);
        Assert.Equal("local", result.Idp);
        Assert.Equal("session-123", result.SessionId);
    }

    [Fact]
    public void BuildMeResponse_WhenNoNameClaim_FallsBackToPreferredUsername()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(JoseClaimNames.Payload.PreferredUsername, "ada")],
                authenticationType: "cookie"
            )
        );

        var result = InteractiveAuthenticationEndpointHandler.BuildMeResponse(
            principal,
            "subject-1"
        );

        Assert.Equal("ada", result.Name);
    }

    [Fact]
    public void BuildMeResponse_WhenNoNameOrUsername_FallsBackToSubjectAndOmitsOptionalClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "cookie"));

        var result = InteractiveAuthenticationEndpointHandler.BuildMeResponse(
            principal,
            "subject-1"
        );

        Assert.Equal("subject-1", result.Name);
        Assert.Null(result.AuthTime);
        Assert.Empty(result.Amr);
        Assert.Null(result.Idp);
        Assert.Null(result.SessionId);
    }

    [Fact]
    public void BuildMeResponse_WhenAuthTimeIsNotANumber_ReturnsNullAuthTime()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(JoseClaimNames.Payload.AuthTime, "not-a-number")],
                authenticationType: "cookie"
            )
        );

        var result = InteractiveAuthenticationEndpointHandler.BuildMeResponse(
            principal,
            "subject-1"
        );

        Assert.Null(result.AuthTime);
    }

    [Fact]
    public void BuildMeResponse_WhenAuthTimeOutOfRange_ReturnsNullAuthTime()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(JoseClaimNames.Payload.AuthTime, "99999999999999")],
                authenticationType: "cookie"
            )
        );

        var result = InteractiveAuthenticationEndpointHandler.BuildMeResponse(
            principal,
            "subject-1"
        );

        Assert.Null(result.AuthTime);
    }

    [Fact]
    public void BuildMeResponse_WhenAuthTimeIsFractional_ReturnsWholeSeconds()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(JoseClaimNames.Payload.AuthTime, "1700000000.75")],
                authenticationType: "cookie"
            )
        );

        var result = InteractiveAuthenticationEndpointHandler.BuildMeResponse(
            principal,
            "subject-1"
        );

        Assert.Equal(1700000000L, result.AuthTime);
    }

    #endregion
}
