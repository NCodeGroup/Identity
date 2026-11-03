// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// An <see cref="IOpenIdResponseFormatter"/> that writes a non-redirectable authorization error as a human-readable
/// HTML page, using the same <see cref="InteractiveAuthenticationPageRenderer"/> markup/bundle seam as the login and
/// consent pages. It participates in the content negotiation performed by <c>OpenIdResult&lt;T&gt;</c>: a browser that
/// explicitly requests <c>text/html</c> receives the styled error page (OpenID Connect Core §3.1.2.4), while
/// programmatic callers keep the JSON body. It handles only an <see cref="IOpenIdError"/>, so other response types are
/// unaffected.
/// </summary>
internal sealed class InteractiveOpenIdErrorResponseFormatter(
    InteractiveBundleReader bundleReader,
    InteractiveSurfacePaths surfacePaths,
    ITenantEnvironmentCatalog tenantEnvironmentCatalog,
    InteractiveLocaleSelector localeSelector
) : IOpenIdResponseFormatter
{
    private InteractiveBundleReader BundleReader { get; } = bundleReader;
    private InteractiveSurfacePaths SurfacePaths { get; } = surfacePaths;
    private ITenantEnvironmentCatalog TenantEnvironmentCatalog { get; } = tenantEnvironmentCatalog;
    private InteractiveLocaleSelector LocaleSelector { get; } = localeSelector;

    /// <inheritdoc />
    public IReadOnlyList<string> MediaTypes { get; } = [OpenIdProtocol.MediaTypes.Html];

    /// <inheritdoc />
    public bool CanFormat(IOpenIdResponse response) => response is IOpenIdError;

    /// <inheritdoc />
    public async ValueTask WriteAsync(
        OpenIdResponseFormatterContext context,
        CancellationToken cancellationToken
    )
    {
        var error = (IOpenIdError)context.Response;

        // A request that resolved no tenant has no branding to embed and no settings to bound the page's locale; the
        // request's own ui_locales still selects it (ADR-1402).
        var settings = context
            .HttpContext.GetOpenIdContextOrDefault()
            ?.Tenant.SettingsProvider.Collection;
        var branding = settings is null
            ? null
            : InteractiveBranding.FromSettings(settings, TenantEnvironmentCatalog);
        var locale = LocaleSelector.Select(context.HttpContext, settings);
        var security = InteractivePageSecurity.Create(branding);

        using var html = InteractiveHtmlResult.CreateBuffer();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            html,
            await BundleReader.TryReadShellAsync(cancellationToken),
            // The page is served at the protocol endpoint, so its base points the bundle's assets at the surface.
            SurfacePaths.GetSurfaceBase(context.HttpContext),
            branding,
            security.Nonce,
            locale,
            error.Code,
            error.Description
        );

        var httpResponse = context.HttpContext.Response;
        if (context.StatusCode.HasValue)
        {
            httpResponse.StatusCode = context.StatusCode.Value;
        }

        await InteractiveHtmlResult.WriteAsync(
            httpResponse,
            html.AsReadOnlySequence,
            security.Policy,
            cancellationToken
        );
    }
}
