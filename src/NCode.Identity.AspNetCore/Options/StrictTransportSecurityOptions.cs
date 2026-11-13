// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.AspNetCore.Options;

/// <summary>
/// Configures the <c>Strict-Transport-Security</c> header (RFC 6797) that the application sends on every response to a
/// request that arrived over HTTPS. Bound from the <c>StrictTransportSecurity</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// The header is sent unless <c>LocalDevelopment:Enabled</c> is set, and never to <c>localhost</c>, <c>127.0.0.1</c>, or
/// <c>[::1]</c>.
/// </para>
/// <para>
/// A request that a trusted reverse proxy received over HTTPS counts as HTTPS, so behind a TLS-terminating proxy either
/// trust the proxy in <c>ForwardedHeaders</c> or let the proxy send the header and set <see cref="Enabled"/> to
/// <c>false</c>.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class StrictTransportSecurityOptions
{
    /// <summary>
    /// The configuration section (<c>StrictTransportSecurity</c>) bound to <see cref="StrictTransportSecurityOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "StrictTransportSecurity";

    /// <summary>
    /// Gets or sets a value indicating whether the application sends the header. Defaults to <c>true</c>. Set it to
    /// <c>false</c> when a proxy in front of the application sends the header, so that one component owns it.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the <c>max-age</c> directive, which is how long a browser remembers to use HTTPS only. Defaults to
    /// 365 days. Zero tells browsers to forget the policy; a negative value stops the host when it starts.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Gets or sets a value indicating whether the <c>includeSubDomains</c> directive is sent. Defaults to
    /// <c>true</c>. Set it to <c>false</c> only when a subdomain of the host is served over plain HTTP.
    /// </summary>
    public bool IncludeSubDomains { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>preload</c> directive is sent. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// The directive commits every subdomain of the registrable domain to HTTPS in the browsers' preload lists and is
    /// slow to undo, so set it only after submitting the domain. It requires <see cref="IncludeSubDomains"/> and a
    /// <see cref="MaxAge"/> of at least 365 days.
    /// </remarks>
    public bool Preload { get; set; }

    /// <summary>
    /// Gets or sets the host names that never receive the header, besides <c>localhost</c>, <c>127.0.0.1</c>, and
    /// <c>[::1]</c>. Each entry is a DNS name, an IPv4 address, or an IPv6 address in brackets, without a scheme or a
    /// port, and is compared with the request's host ignoring case. Empty by default.
    /// </summary>
    public string[] ExcludedHosts { get; set; } = [];
}
