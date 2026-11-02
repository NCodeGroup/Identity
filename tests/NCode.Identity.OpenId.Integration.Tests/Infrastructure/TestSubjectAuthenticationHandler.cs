// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A minimal test authentication scheme that authenticates a subject from an <c>X-Test-Subject</c> request header
/// (the header value becomes the <c>sub</c> claim). When the header is absent the request is treated as anonymous.
/// This stands in for a real bearer/cookie scheme that the host would configure, so endpoints that authenticate a
/// subject (such as <c>UserInfo</c>) can be exercised end-to-end.
/// </summary>
public class TestSubjectAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string SubjectHeaderName = "X-Test-Subject";
    public const string ScopeHeaderName = "X-Test-Scope";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (
            !Request.Headers.TryGetValue(SubjectHeaderName, out var values)
            || values.ToString() is not { Length: > 0 } subjectId
        )
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim("sub", subjectId));

        // Mirror the self-issued access token's space-delimited 'scope' claim (RFC 9068) so the scope-driven UserInfo
        // enricher has the granted scopes to act on, just as it would from a real bearer token.
        if (
            Request.Headers.TryGetValue(ScopeHeaderName, out var scopeValues)
            && scopeValues.ToString() is { Length: > 0 } scope
        )
        {
            identity.AddClaim(new Claim("scope", scope));
        }

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
