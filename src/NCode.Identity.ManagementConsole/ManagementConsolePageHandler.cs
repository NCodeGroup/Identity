// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.AspNetCore;
using NCode.Buffers;
using NCode.Identity.Bff;
using NCode.Identity.ManagementConsole.Options;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Serves the console bundle's <c>index.html</c> for every page request. Each response carries a fresh nonce, written
/// in place of the bundle's nonce placeholder, and the Content-Security-Policy that allows only that nonce. The page is
/// never cached, because its nonce is unique to the response.
/// </summary>
internal sealed class ManagementConsolePageHandler(
    SinglePageBundle<ManagementConsolePage> bundle,
    IBffOptionsAccessor bffOptionsAccessor,
    IOptions<ManagementConsoleOptions> optionsAccessor
)
{
    /// <summary>
    /// Writes the console page, or a <c>503</c> problem when the bundle has not been built.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The console page, or the problem that reports a missing bundle.</returns>
    public async Task<IResult> HandleAsync(HttpContext httpContext)
    {
        var page = await bundle.TryReadIndexAsync(httpContext.RequestAborted);
        if (page is null)
        {
            httpContext.Response.SetNoStore();
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The management console is not built.",
                detail: "Build apps/mgmt (pnpm --filter @ncode/app-mgmt build) and set ManagementConsole:BundleRootPath to its dist directory."
            );
        }

        var authority = new Uri(
            bffOptionsAccessor.Current.Frontends[ManagementConsoleDefaults.FrontendName].Authority
        );
        var security = ManagementConsolePageSecurity.Create(
            authority,
            optionsAccessor.Value.Branding.LogoUrl
        );

        httpContext.Response.SetHtmlPageSecurity(security.Policy);
        return TypedResults.Text(
            page.Render(security.Nonce, null),
            "text/html",
            contentEncoding: SecureEncoding.UTF8
        );
    }
}
