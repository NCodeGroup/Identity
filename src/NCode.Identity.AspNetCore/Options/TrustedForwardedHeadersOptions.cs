// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.AspNetCore.Options;

/// <summary>
/// Configures which reverse proxies the application trusts to describe the original request through
/// <c>X-Forwarded-For</c>, <c>X-Forwarded-Proto</c>, and <c>X-Forwarded-Host</c>. Bound from the <c>ForwardedHeaders</c>
/// configuration section.
/// </summary>
/// <remarks>
/// <para>
/// An application behind a TLS-terminating proxy sees a plaintext HTTP connection to an internal address. Without these
/// headers it reads the wrong scheme, host and client address: an HTTPS-only policy (the HTTPS redirect and
/// Strict-Transport-Security) never applies, host-based tenant selection and the choice among a tenant's own published
/// origins see the container-internal name, and the client address is the proxy's.
/// </para>
/// <para>
/// Trusting those headers from an untrusted peer is the mirror-image failure: a client that can reach the application
/// directly could assert any scheme, host and client address and make the application select a tenant by a host it
/// chose, treat a request as HTTPS that was not, or key a rate limit and an audit record on an address it chose.
/// Forwarding is therefore <b>off unless a trusted proxy is configured</b>, and it is never enabled by
/// trusting every peer — leave both lists empty for a direct-TLS deployment, where the connection already carries the
/// truth.
/// </para>
/// <para>
/// Configuring a trusted proxy is necessary but not sufficient: the proxy itself must overwrite these headers on
/// inbound requests rather than pass a client-supplied value through, and the host must still be constrained with the
/// <c>AllowedHosts</c> setting so a trusted proxy cannot be induced to forward an arbitrary host.
/// </para>
/// </remarks>
// See ADR-1447.
[PublicAPI]
public sealed class TrustedForwardedHeadersOptions
{
    /// <summary>
    /// The configuration section (<c>ForwardedHeaders</c>) bound to <see cref="TrustedForwardedHeadersOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "ForwardedHeaders";

    /// <summary>
    /// Gets or sets the individual proxy IP addresses whose forwarded headers are trusted (for example
    /// <c>["10.0.0.4"]</c>). An IPv4 address is written as four decimal octets; an entry that is not an IP address stops
    /// the host when it starts. Empty by default, which — together with an empty <see cref="KnownNetworks"/> — disables
    /// forwarded-header processing entirely.
    /// </summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>
    /// Gets or sets the proxy networks, in CIDR notation, whose forwarded headers are trusted (for example
    /// <c>["172.16.0.0/12"]</c> for a container bridge network, where the proxy's address is assigned dynamically and
    /// cannot be pinned in <see cref="KnownProxies"/>). The address must be the network's first address; an entry that
    /// is not a network in CIDR notation stops the host when it starts.
    /// </summary>
    /// <remarks>
    /// Keep this as tight as the deployment allows. A range such as <c>0.0.0.0/0</c> trusts every peer and reintroduces
    /// exactly the spoofing the allow-list exists to prevent.
    /// </remarks>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// Gets or sets how many proxy hops are consulted when reading a forwarded header. Defaults to <c>1</c>, matching a
    /// single reverse proxy in front of the application; a value below <c>1</c> stops the host when it starts.
    /// </summary>
    /// <remarks>
    /// The limit is what stops a client from prepending its own entries: with a limit of <c>1</c> only the value the
    /// trusted proxy appended is honored, and anything the client sent ahead of it is left alone. Raise it only to the
    /// actual number of chained proxies that each overwrite these headers.
    /// </remarks>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>
    /// Gets a value indicating whether any trusted proxy is configured, and therefore whether forwarded-header
    /// processing should be enabled at all.
    /// </summary>
    public bool HasTrustedProxy => KnownProxies.Length > 0 || KnownNetworks.Length > 0;
}
