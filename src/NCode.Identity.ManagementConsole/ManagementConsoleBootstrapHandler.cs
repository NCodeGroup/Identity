// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.AspNetCore;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Answers the request for the console's runtime bootstrap document at
/// <see cref="ManagementConsoleDefaults.BootstrapPath"/>, which the bundle fetches at boot to discover its API base,
/// its BFF's coordinates, its feature flags and its branding.
/// </summary>
internal sealed class ManagementConsoleBootstrapHandler(
    IOptions<ManagementConsoleOptions> optionsAccessor,
    IOptions<BffOptions> bffOptionsAccessor
)
{
    /// <summary>
    /// Writes the bootstrap document as a non-cacheable JSON response.
    /// </summary>
    public IResult Handle(HttpContext httpContext)
    {
        httpContext.Response.SetNoStore();

        var document = ManagementConsoleBootstrapDocument.Create(
            optionsAccessor.Value,
            bffOptionsAccessor.Value,
            httpContext.Request.PathBase
        );

        return TypedResults.Json(document, JsonSerializerOptions.Web);
    }
}
