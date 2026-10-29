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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Jose;

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// Handles <c>GET /bff/user</c>, which tells the frontend who is signed in without exposing any token.
/// </summary>
internal class BffUserHandler(BffSessionResolver resolver, IOptions<BffOptions> options)
{
    private const string JsonContentType = "application/json; charset=utf-8";
    private const string LogoutPath = "/logout";

    private readonly BffSessionResolver _resolver = resolver;
    private readonly IOptions<BffOptions> _options = options;

    /// <summary>
    /// Writes the signed-in user, or a <c>401</c> problem when the request has no session.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    public virtual async Task HandleAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var response = httpContext.Response;
        BffResponses.SetNoStore(response);

        var cancellationToken = httpContext.RequestAborted;
        var session = await _resolver.ResolveAsync(httpContext, cancellationToken);
        if (session is null)
        {
            await BffResponses.WriteProblemAsync(
                httpContext,
                StatusCodes.Status401Unauthorized,
                BffProblemTypes.SessionRequired,
                "A BFF session is required."
            );
            return;
        }

        var bffOptions = _options.Value;
        var principal = BffSessionPrincipal.Create(session.Payload.Claims, session.Metadata.Issuer);

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = JsonContentType;

        await using var writer = new Utf8JsonWriter(response.BodyWriter);
        writer.WriteStartObject();

        writer.WriteString(JoseClaimNames.Payload.Sub, session.Metadata.Subject);
        var name = principal.FindFirst(JoseClaimNames.Payload.Name)?.Value;
        if (name is not null)
            writer.WriteString(JoseClaimNames.Payload.Name, name);

        writer.WriteStartObject("claims");
        foreach (var claimType in bffOptions.UserClaims)
        {
            var values = principal.FindAll(claimType).Select(claim => claim.Value).ToList();
            if (values.Count == 0)
                continue;

            if (values.Count == 1)
            {
                writer.WriteString(claimType, values[0]);
                continue;
            }

            writer.WriteStartArray(claimType);
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();

        writer.WriteStartObject("session");
        writer.WriteNumber("expiresAt", session.Metadata.AbsoluteExpiresAt.ToUnixTimeSeconds());
        writer.WriteNumber("idleExpiresAt", session.Metadata.IdleExpiresAt.ToUnixTimeSeconds());
        writer.WriteEndObject();

        var logoutPath = BffNavigation.LocalUrl(
            httpContext.Request,
            bffOptions.PathPrefix + LogoutPath
        );
        var guard = Uri.EscapeDataString(session.Payload.LogoutGuard);
        writer.WriteString("logoutUrl", $"{logoutPath}?sid={guard}");

        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
        await response.BodyWriter.FlushAsync(cancellationToken);
    }
}
