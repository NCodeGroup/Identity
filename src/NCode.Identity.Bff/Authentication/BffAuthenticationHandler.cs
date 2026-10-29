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

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Authentication;

/// <summary>
/// Authenticates a request from its BFF session cookie, and answers challenges and forbids.
/// </summary>
internal class BffAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    BffSessionResolver resolver,
    IOptions<BffOptions> options
) : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder)
{
    private const string LoginPath = "/login";

    private readonly BffSessionResolver _resolver = resolver;
    private readonly IOptions<BffOptions> _options = options;

    private bool IsApiEndpoint =>
        Context.GetEndpoint()?.Metadata.GetMetadata<BffApiAttribute>() is not null;

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var session = await _resolver.ResolveAsync(Context, Context.RequestAborted);
        if (session is null)
            return AuthenticateResult.NoResult();

        var feature = Context.Features.Get<BffRequestFeature>();
        if (feature?.Principal is not { } principal)
            return AuthenticateResult.NoResult();

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (IsApiEndpoint)
        {
            return BffResponses.WriteProblemAsync(
                Context,
                StatusCodes.Status401Unauthorized,
                BffProblemTypes.SessionRequired,
                "A BFF session is required."
            );
        }

        var bffOptions = _options.Value;
        var request = Request;
        var returnUrl = $"{request.Path}{request.QueryString}";
        if (!BffReturnUrls.IsAllowed(returnUrl, bffOptions.ReturnUrls))
            returnUrl = "/";

        var loginUrl = BffNavigation.LocalUrl(request, bffOptions.PathPrefix + LoginPath);
        var escaped = Uri.EscapeDataString(returnUrl);
        Response.Redirect($"{loginUrl}?returnUrl={escaped}");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        if (!IsApiEndpoint)
            return base.HandleForbiddenAsync(properties);

        return BffResponses.WriteProblemAsync(
            Context,
            StatusCodes.Status403Forbidden,
            BffProblemTypes.Forbidden,
            "Access to this resource is forbidden."
        );
    }
}
