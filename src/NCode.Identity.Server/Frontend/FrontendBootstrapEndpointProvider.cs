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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.Server.Frontend;

/// <summary>
/// Maps the frontend runtime bootstrap document (ADR-0064) at <see cref="EndpointPath"/>. Registered as an
/// <see cref="IEndpointProvider"/> so the host's single <c>MapEndpointGroups</c> call maps it alongside every other
/// endpoint; the tier-agnostic bundles fetch this document from their serving host at boot to discover their
/// deployment coordinates (API base, issuer, client id, feature flags, branding), so one bundle serves every tier.
/// </summary>
internal sealed class FrontendBootstrapEndpointProvider : IEndpointProvider
{
    /// <summary>
    /// The well-known path, relative to the serving host, at which the frontend bundles fetch the bootstrap document.
    /// </summary>
    private const string EndpointPath = "/config/bootstrap.json";

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(EndpointPath, GetFrontendBootstrap)
            .WithName("frontend_bootstrap_endpoint")
            .AllowAnonymous();

    private static JsonHttpResult<FrontendBootstrapDocument> GetFrontendBootstrap(
        HttpContext httpContext,
        IOptions<FrontendBootstrapOptions> optionsAccessor
    )
    {
        var request = httpContext.Request;
        var requestOrigin = $"{request.Scheme}://{request.Host}";

        // Every surface shares this one endpoint and names itself via the `surface` query parameter so the host can
        // return that surface's own coordinates (for example, each SPA's public client id); absent, the default is served.
        var surface = request.Query["surface"].ToString();
        var document = FrontendBootstrapDocument.Create(
            optionsAccessor.Value,
            requestOrigin,
            string.IsNullOrEmpty(surface) ? null : surface
        );

        return TypedResults.Json(document, JsonSerializerOptions.Web);
    }
}
