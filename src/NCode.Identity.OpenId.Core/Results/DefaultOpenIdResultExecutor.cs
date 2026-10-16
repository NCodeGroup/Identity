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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Results;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Provides the default implementation of <see cref="IResultExecutor{T}"/> for <see cref="IOpenIdResult"/>. It applies
/// the response envelope (status code and any <see cref="ISupportResponseHeaders"/>), then performs server-driven
/// content negotiation over the registered <see cref="IOpenIdResponseFormatter"/> collection and lets the selected
/// formatter write the body. The default JSON representation is itself a formatter
/// (<see cref="DefaultJsonOpenIdResponseFormatter"/>), so every response — including the fallback — flows through the
/// same pipeline; an interactive client that explicitly requests an alternate media type (for example an HTML error
/// page) is served by its formatter instead.
/// </summary>
internal sealed class DefaultOpenIdResultExecutor : IResultExecutor<IOpenIdResult>
{
    /// <inheritdoc />
    public async ValueTask ExecuteAsync(
        HttpContext httpContext,
        IOpenIdResult result,
        CancellationToken cancellationToken
    )
    {
        var response = result.Response;
        var statusCode = response is ISupportStatusCode supportStatusCode
            ? supportStatusCode.StatusCode
            : null;

        if (response is ISupportResponseHeaders { ResponseHeaders: { } responseHeaders })
        {
            var headers = httpContext.Response.Headers;
            foreach (var (name, value) in responseHeaders)
            {
                headers[name] = value;
            }
        }

        var formatters = httpContext.RequestServices.GetServices<IOpenIdResponseFormatter>();
        var (formatter, mediaType) = SelectFormatter(formatters, httpContext.Request, response);

        await formatter.WriteAsync(
            new OpenIdResponseFormatterContext
            {
                HttpContext = httpContext,
                Response = response,
                MediaType = mediaType,
                StatusCode = statusCode,
            },
            cancellationToken
        );
    }

    // Server-driven content negotiation: picks the formatter for the highest-quality media type the client EXPLICITLY
    // lists (wildcards are ignored, so "*/*" and programmatic callers never select an alternate representation) that
    // both supports that media type and can format this response; the application/json default is appended as the final
    // candidate so a non-matching or wildcard-only Accept resolves to the default JSON formatter.
    private static (IOpenIdResponseFormatter Formatter, string MediaType) SelectFormatter(
        IEnumerable<IOpenIdResponseFormatter> formatters,
        HttpRequest request,
        IOpenIdResponse response
    )
    {
        var candidates = formatters as IReadOnlyList<IOpenIdResponseFormatter> ?? [.. formatters];

        // The protocol default (application/json) is appended as the final candidate so a wildcard-only or non-matching
        // Accept resolves to the default JSON formatter.
        var requestedMediaTypes = RankExplicitMediaTypes(request)
            .Append(OpenIdConstants.MediaTypes.Json);

        foreach (var requested in requestedMediaTypes)
        {
            foreach (var formatter in candidates)
            {
                if (!formatter.CanFormat(response))
                {
                    continue;
                }

                foreach (var supported in formatter.MediaTypes)
                {
                    if (supported.Equals(requested, StringComparison.OrdinalIgnoreCase))
                    {
                        return (formatter, supported);
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"No '{OpenIdConstants.MediaTypes.Json}' {nameof(IOpenIdResponseFormatter)} is registered to render the OpenID response."
        );
    }

    // The client's EXPLICITLY listed, acceptable (q > 0) media types ordered by descending quality; wildcard ranges
    // ("*/*", "text/*") are excluded so only a deliberate media-type request selects an alternate representation.
    private static IEnumerable<string> RankExplicitMediaTypes(HttpRequest request)
    {
        var accept = request.GetTypedHeaders().Accept;
        if (accept is not { Count: > 0 })
        {
            return [];
        }

        return accept
            .Where(mediaType =>
                !mediaType.Type.Equals("*", StringComparison.Ordinal)
                && !mediaType.SubType.Equals("*", StringComparison.Ordinal)
                && (mediaType.Quality ?? 1.0) > 0
            )
            .OrderByDescending(mediaType => mediaType.Quality ?? 1.0)
            .Select(mediaType => $"{mediaType.Type}/{mediaType.SubType}");
    }
}
