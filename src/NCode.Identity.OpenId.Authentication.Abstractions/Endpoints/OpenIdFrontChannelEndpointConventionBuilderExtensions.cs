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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides extension methods that mark front-channel endpoints and recognize them.
/// </summary>
[PublicAPI]
public static class OpenIdFrontChannelEndpointConventionBuilderExtensions
{
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/> instance.</param>
    /// <typeparam name="TBuilder">The type of the <see cref="IEndpointConventionBuilder"/> instance.</typeparam>
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Adds <see cref="IOpenIdEndpointFrontChannelMetadata"/> to the <see cref="IEndpointConventionBuilder"/> so
        /// that no CORS policy applies to the endpoint: the user agent navigates to it, and a script never calls it.
        /// The endpoint also receives its CORS preflight requests and refuses them with <c>405</c>, so a CORS
        /// middleware never answers them either.
        /// </summary>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithOpenIdFrontChannel()
        {
            builder.WithMetadata(new OpenIdEndpointFrontChannelMetadata());
            builder.Finally(RefuseCorsPreflight);
            return builder;
        }
    }

    /// <param name="endpoint">The <see cref="Endpoint"/> instance, or <see langword="null"/> when none is selected.</param>
    extension(Endpoint? endpoint)
    {
        /// <summary>
        /// Gets a value indicating whether the endpoint is a front-channel endpoint that no CORS policy applies to: its
        /// last <see cref="IOpenIdEndpointFrontChannelMetadata"/> sets <see cref="IOpenIdEndpointFrontChannelMetadata.IsFrontChannel"/>.
        /// </summary>
        public bool IsOpenIdFrontChannel =>
            endpoint?.Metadata.GetMetadata<IOpenIdEndpointFrontChannelMetadata>()?.IsFrontChannel
            ?? false;
    }

    // Routing sends a CORS preflight request only to an endpoint that accepts it and otherwise selects its own 405
    // endpoint, which carries no metadata, so a CORS middleware would answer the preflight. Accepting the preflight
    // here lets the CORS gate see the marker, and the endpoint then refuses the preflight as routing would have.
    private static void RefuseCorsPreflight(EndpointBuilder endpointBuilder)
    {
        var metadata = endpointBuilder.Metadata;
        var allowedMethods = new List<string>();
        for (var index = 0; index < metadata.Count; index++)
        {
            if (metadata[index] is not IHttpMethodMetadata httpMethodMetadata)
            {
                continue;
            }

            allowedMethods.AddRange(httpMethodMetadata.HttpMethods);
            if (!httpMethodMetadata.AcceptCorsPreflight)
            {
                metadata[index] = new HttpMethodMetadata(
                    httpMethodMetadata.HttpMethods,
                    acceptCorsPreflight: true
                );
            }
        }

        var next = endpointBuilder.RequestDelegate;
        if (next is null)
        {
            return;
        }

        var allow = string.Join(", ", allowedMethods.Distinct(StringComparer.OrdinalIgnoreCase));
        endpointBuilder.RequestDelegate = httpContext =>
        {
            var request = httpContext.Request;
            if (
                !HttpMethods.IsOptions(request.Method)
                || !request.Headers.ContainsKey(HeaderNames.AccessControlRequestMethod)
            )
            {
                return next(httpContext);
            }

            httpContext.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            httpContext.Response.Headers.Allow = allow;
            return Task.CompletedTask;
        };
    }
}
