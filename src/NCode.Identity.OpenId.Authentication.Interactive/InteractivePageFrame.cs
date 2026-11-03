// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The document every page of the interactive surface is written into: the bundle the page mounts, the base its
/// relative URLs resolve against, the branding the bundle themes itself with, the nonce of its Content-Security-Policy
/// and the locale it renders in. A page's own markup is written between the frame's head and tail.
/// </summary>
/// <param name="Shell">The bundle's split <c>index.html</c>, or <see langword="null"/> for a standalone page.</param>
/// <param name="SurfaceBase">The URL of the surface beneath the tenant, which the page's base is set to.</param>
/// <param name="Branding">The tenant's branding, embedded for the bundle; <see langword="null"/> embeds none.</param>
/// <param name="Nonce">The nonce of the page's Content-Security-Policy, written in place of the bundle's nonce
/// placeholder.</param>
/// <param name="Locale">The locale the page renders in, whose language tag the document's <c>html</c> element and the
/// page's form carry.</param>
// See ADR-1253, ADR-1256 and ADR-1402.
internal sealed record InteractivePageFrame(
    InteractiveBundleShell? Shell,
    string SurfaceBase,
    InteractiveBranding? Branding,
    string Nonce,
    InteractiveLocale Locale
);
