// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.AspNetCore;
using NCode.Identity.Bff.Responses;

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// Handles <c>GET /bff/user</c>, which tells the frontend who is signed in without exposing any token. The document
/// comes from the registered <see cref="IBffUserDocumentFactory"/>.
/// </summary>
internal class BffUserHandler(IBffUserDocumentFactory documentFactory)
{
    private const string JsonContentType = "application/json; charset=utf-8";

    /// <summary>
    /// Writes the signed-in user, or a <c>401</c> problem when the request has no session.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    public virtual async Task HandleAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var response = httpContext.Response;
        response.SetNoStore();

        var cancellationToken = httpContext.RequestAborted;
        var document = await documentFactory.CreateAsync(httpContext, cancellationToken);
        if (document is null)
        {
            await BffResponses.WriteProblemAsync(
                httpContext,
                StatusCodes.Status401Unauthorized,
                BffProblemTypes.SessionRequired,
                "A BFF session is required."
            );
            return;
        }

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = JsonContentType;

        await using var writer = new Utf8JsonWriter(response.BodyWriter);
        document.Value.WriteTo(writer);
        await writer.FlushAsync(cancellationToken);
        await response.BodyWriter.FlushAsync(cancellationToken);
    }
}
