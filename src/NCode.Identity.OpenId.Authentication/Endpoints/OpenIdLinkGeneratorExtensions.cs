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

using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Generates the absolute URLs the OpenID Provider publishes, anchored to the tenant's base address rather than to the
/// incoming request.
/// </summary>
/// <remarks>
/// <para>
/// Every URL this server hands to a client — the discovery endpoints, the RFC 7592 <c>registration_client_uri</c>, the
/// device <c>verification_uri</c>, the <c>check_session_iframe</c>, and the continue URL — tells that client where to
/// send credentials. Generating them from the incoming request means a caller's <c>Host</c> header chooses that
/// destination: a request carrying a foreign host yields a discovery document whose <c>issuer</c> is correct while its
/// <c>token_endpoint</c> points elsewhere, and a conforming client will post its client secret and authorization code
/// to that endpoint.
/// </para>
/// <para>
/// Anchoring to the tenant's <c>BaseAddress</c> removes that choice from the caller. The base address is the
/// tenant's issuer where one is set; only when it is unset does it fall back to the request, which is a
/// development-only convenience.
/// </para>
/// <para>
/// <b>Use this rather than <see cref="LinkGenerator"/> directly</b> for any URL that leaves the process.
/// </para>
/// </remarks>
internal static class OpenIdLinkGeneratorExtensions
{
    extension(LinkGenerator linkGenerator)
    {
        /// <summary>
        /// Generates an absolute URL for the named endpoint using the scheme and host of the tenant's base address.
        /// </summary>
        /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
        /// <param name="endpointName">The name of the endpoint to generate a URL for.</param>
        /// <param name="values">The route values, if any.</param>
        /// <returns>The absolute URL, or <c>null</c> when the endpoint cannot be resolved.</returns>
        public string? GetTenantUriByName(
            OpenIdContext openIdContext,
            string endpointName,
            object? values = null
        )
        {
            ArgumentNullException.ThrowIfNull(linkGenerator);
            ArgumentNullException.ThrowIfNull(openIdContext);

            var httpContext = openIdContext.Http;
            var baseAddress = openIdContext.Tenant.BaseAddress;
            var explicitValues = new RouteValueDictionary(values);

            // The path still comes from routing (which includes any PathBase the host is mounted under); only the
            // authority is taken from the tenant, because that is the part the caller could otherwise dictate. The
            // request's route values are supplied as ambient values so a parameter of the tenant route the endpoints
            // are mounted under (for example {tenantId}) carries over into the URL; generating by name alone would
            // drop it, and an ambient value never becomes a query parameter.
            return linkGenerator.GetUriByAddress(
                httpContext,
                endpointName,
                explicitValues,
                ambientValues: httpContext.Request.RouteValues,
                scheme: baseAddress.Scheme,
                host: baseAddress.Host
            );
        }
    }
}
