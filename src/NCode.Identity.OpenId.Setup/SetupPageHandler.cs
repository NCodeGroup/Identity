// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.AspNetCore;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Serves the setup bundle's <c>index.html</c> and its content-hashed assets. Each page response carries a fresh nonce,
/// written in place of the bundle's nonce placeholder, and the Content-Security-Policy that allows only that nonce. The
/// page is never cached, because its nonce is unique to the response. Every route answers <c>404</c> once the instance
/// is initialized, and in Production when the request is not HTTPS, so the surface does not exist for either.
/// </summary>
internal sealed class SetupPageHandler(
    IOptions<SetupOptions> optionsAccessor,
    ISetupSurfaceGate surfaceGate
) : IDisposable
{
    /// <summary>
    /// The name of the route value that carries the path of an asset beneath the bundle's assets directory.
    /// </summary>
    public const string AssetRouteParameterName = "path";

    private SinglePageBundle<SetupPage> Bundle { get; } =
        new(optionsAccessor.Value.BundleRootPath, static html => new SetupPage(html));

    private ISetupSurfaceGate SurfaceGate { get; } = surfaceGate;

    /// <inheritdoc />
    public void Dispose() => Bundle.Dispose();

    /// <summary>
    /// Writes the setup page, or <c>404</c> when the instance is initialized, when a Production request is not HTTPS,
    /// or when no bundle is readable.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The setup page, or <c>404</c> when the surface is not served.</returns>
    public async ValueTask<IResult> HandlePageAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (!await SurfaceGate.IsServedAsync(httpContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var page = await Bundle.TryReadIndexAsync(cancellationToken);
        if (page is null)
        {
            return TypedResults.NotFound();
        }

        var console = optionsAccessor.Value.Wizard.ManagementConsole;
        var managementConsoleOrigin = console is { IsConfigured: true, BaseUrl: { } baseUrl }
            ? new Uri(baseUrl)
            : null;

        var security = SetupPageSecurity.Create(managementConsoleOrigin);
        httpContext.Response.SetHtmlPageSecurity(security.Policy);

        var html = page.Render(security.Nonce, httpContext.Request.PathBase);
        return TypedResults.Text(html, "text/html", contentEncoding: SecureEncoding.UTF8);
    }

    /// <summary>
    /// Serves a file of the setup bundle's assets directory, such as a hashed script or style, named by the
    /// <see cref="AssetRouteParameterName"/> route value, or <c>404</c> when the instance is initialized, when a
    /// Production request is not HTTPS, or when there is no such file.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The file, or <c>404</c> when the surface is not served or there is no such file.</returns>
    public async ValueTask<IResult> HandleAssetAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (!await SurfaceGate.IsServedAsync(httpContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var assetPath =
            httpContext.GetRouteValue(AssetRouteParameterName) as string ?? string.Empty;
        return Bundle.ServeAsset(httpContext.Response, assetPath);
    }
}
