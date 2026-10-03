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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Results;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the <c>OpenID Connect</c>
/// <c>UserInfo</c> endpoint
/// (<see href="https://openid.net/specs/openid-connect-core-1_0.html#UserInfo">OpenID Connect Core 5.3</see>). The
/// endpoint is a thin HTTP shell: it authenticates the subject via the <see cref="AuthenticateSubjectCommand"/>
/// mediator command, then delegates to the <see cref="GetUserInfoClaimsCommand"/> so that the default and
/// application-provided enrichers contribute the subject's claims.
/// </summary>
internal class DefaultUserInfoEndpointProvider : IEndpointProvider
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapMethods(
                OpenIdConstants.EndpointPaths.UserInfo,
                [HttpMethods.Get, HttpMethods.Post],
                HandleRouteAsync
            )
            .WithName(OpenIdConstants.EndpointNames.UserInfo)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

    internal virtual async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;

        var disposition = await mediator.SendAsync<
            AuthenticateSubjectCommand,
            AuthenticateSubjectDisposition
        >(new AuthenticateSubjectCommand(openIdContext), cancellationToken);

        if (disposition.HasError)
        {
            var error = disposition.Error;
            error.StatusCode ??= StatusCodes.Status401Unauthorized;
            return error.AsHttpResult();
        }

        if (!disposition.IsAuthenticated)
        {
            return errorFactory
                .InvalidToken()
                .WithStatusCode(StatusCodes.Status401Unauthorized)
                .AsHttpResult();
        }

        var result = new UserInfoResult();

        await mediator.SendAsync(
            new GetUserInfoClaimsCommand(openIdContext, disposition.Ticket.Value, result.Claims),
            cancellationToken
        );

        return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
    }
}
