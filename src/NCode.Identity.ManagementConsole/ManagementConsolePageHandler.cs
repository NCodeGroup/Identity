// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.AspNetCore;
using NCode.Identity.Bff;
using NCode.Identity.ManagementConsole.Options;
using NCode.Identity.OpenId;

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
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The console page, or the problem that reports a missing bundle.</returns>
    public async ValueTask<IResult> HandleAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var page = await bundle.TryReadIndexAsync(cancellationToken);
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
        var branding = optionsAccessor.Value.Branding;
        var security = ManagementConsolePageSecurity.Create(
            authority,
            branding.LogoUrl,
            branding.FaviconUrl
        );

        httpContext.Response.SetHtmlPageSecurity(security.Policy);
        var faviconUrl = AbsoluteUri.TryParseHttps(branding.FaviconUrl, out _)
            ? branding.FaviconUrl
            : null;
        var html = page.Render(security.Nonce, faviconUrl);
        return TypedResults.Text(html, "text/html", contentEncoding: SecureEncoding.UTF8);
    }
}
