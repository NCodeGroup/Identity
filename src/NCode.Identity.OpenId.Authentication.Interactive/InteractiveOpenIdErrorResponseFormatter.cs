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

using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;

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
    InteractiveSurfacePaths surfacePaths
) : IOpenIdResponseFormatter
{
    private InteractiveBundleReader BundleReader { get; } = bundleReader;
    private InteractiveSurfacePaths SurfacePaths { get; } = surfacePaths;

    /// <inheritdoc />
    public IReadOnlyList<string> MediaTypes { get; } = [OpenIdConstants.MediaTypes.Html];

    /// <inheritdoc />
    public bool CanFormat(IOpenIdResponse response) => response is IOpenIdError;

    /// <inheritdoc />
    public async ValueTask WriteAsync(
        OpenIdResponseFormatterContext context,
        CancellationToken cancellationToken
    )
    {
        var error = (IOpenIdError)context.Response;

        // A request that resolved no tenant has no branding to embed.
        var branding = context.HttpContext.GetOpenIdContextOrDefault() is { } openIdContext
            ? InteractiveBranding.FromSettings(openIdContext.Tenant.SettingsProvider.Collection)
            : null;
        var security = InteractivePageSecurity.Create(branding);

        using var html = InteractiveHtmlResult.CreateBuffer();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            html,
            await BundleReader.TryReadShellAsync(cancellationToken),
            // The page is served at the protocol endpoint, so its base points the bundle's assets at the surface.
            SurfacePaths.GetSurfaceBase(context.HttpContext),
            branding,
            security.Nonce,
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
