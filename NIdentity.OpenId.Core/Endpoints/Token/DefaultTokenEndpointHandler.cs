#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using NIdentity.OpenId.Endpoints.Token.Messages;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Servers;

namespace NIdentity.OpenId.Endpoints.Token;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the token endpoint.
/// </summary>
public class DefaultTokenEndpointHandler(
    OpenIdServer openIdServer,
    IOpenIdContextFactory contextFactory
) : IOpenIdEndpointProvider
{
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapGet(OpenIdConstants.EndpointPaths.Token, HandleRouteAsync)
        .WithName(OpenIdConstants.EndpointNames.Token)
        .OpenIdDiscoverable();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header) &&
        header.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);

    private async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var errorFactory = OpenIdServer.ErrorFactory;

        var isPostVerb = httpContext.Request.Method == HttpMethods.Post;
        if (!isPostVerb || !IsApplicationFormContentType(httpContext))
        {
            return TypedResults.BadRequest();
        }

        var openIdContext = await ContextFactory.CreateContextAsync(
            httpContext,
            mediator,
            cancellationToken);

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);

        var tokenRequest = TokenRequest.Load(OpenIdServer, formData);

        // TODO: create a pattern to parse client_id/secret from: Basic, Form, Assertion, mutual TLS, or anything else.

        if (IsBasicAuth(httpContext, out var username, out var password))
        {
            if (!string.IsNullOrEmpty(tokenRequest.ClientId) &&
                !string.Equals(tokenRequest.ClientId, username, StringComparison.Ordinal))
            {
                return TypedResults.BadRequest();
            }

            tokenRequest.ClientId = username;
            tokenRequest.ClientSecret = password;
        }

        // TODO: load client
        // TODO: validate client
        // TODO: validate request
        // TODO: check DPoP
        // TODO: issue token(s)

        throw new NotImplementedException();
    }

    private static bool IsBasicAuth(
        HttpContext httpContext,
        [MaybeNullWhen(false)] out string username,
        [MaybeNullWhen(false)] out string password)
    {
        var authorizationHeader = httpContext.Request.Headers.Authorization;
        if (authorizationHeader.Count != 1)
        {
            username = null;
            password = null;
            return false;
        }

        var authorizationValue = authorizationHeader[0];
        if (string.IsNullOrEmpty(authorizationValue))
        {
            username = null;
            password = null;
            return false;
        }

        const string prefix = "Basic ";
        if (!authorizationValue.StartsWith(prefix, StringComparison.Ordinal))
        {
            username = null;
            password = null;
            return false;
        }

        var token = authorizationValue[prefix.Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            username = null;
            password = null;
            return false;
        }

        // TODO: use memory efficient decoding
        var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        if (string.IsNullOrEmpty(credentials))
        {
            username = null;
            password = null;
            return false;
        }

        var parts = credentials.Split(':');
        if (parts.Length != 2)
        {
            username = null;
            password = null;
            return false;
        }

        username = parts[0];
        password = parts[1];
        return true;
    }
}
