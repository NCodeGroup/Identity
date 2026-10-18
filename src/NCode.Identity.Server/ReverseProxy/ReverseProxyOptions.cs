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

using JetBrains.Annotations;

namespace NCode.Identity.Server.ReverseProxy;

/// <summary>
/// Configures which reverse proxies the OpenID server trusts to describe the original request through
/// <c>X-Forwarded-For</c>, <c>X-Forwarded-Proto</c>, and <c>X-Forwarded-Host</c>. Bound from the <c>ReverseProxy</c>
/// configuration section.
/// </summary>
/// <remarks>
/// <para>
/// This matters more here than in a typical application because the server derives a tenant's base address — and
/// therefore its <c>issuer</c> and every endpoint URL it publishes in discovery — from the scheme and host of the
/// incoming request. When TLS terminates at a proxy, the connection the application sees is plaintext HTTP to an
/// internal address, so without these headers the server would advertise an <c>http://</c> issuer pointing at a
/// container-internal name.
/// </para>
/// <para>
/// Trusting those headers from an untrusted peer is the mirror-image failure: a client that can reach the application
/// directly could assert any scheme and host and make the server mint tokens whose <c>iss</c> it chose. Forwarding is
/// therefore <b>off unless a trusted proxy is configured</b>, and it is never enabled by trusting every peer — leave
/// both lists empty for a direct-TLS deployment, where the connection already carries the truth.
/// </para>
/// <para>
/// Configuring a trusted proxy is necessary but not sufficient: the proxy itself must overwrite these headers on
/// inbound requests rather than pass a client-supplied value through, and the host must still be constrained with the
/// <c>AllowedHosts</c> setting so a trusted proxy cannot be induced to forward an arbitrary host.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class ReverseProxyOptions
{
    /// <summary>
    /// The configuration section (<c>ReverseProxy</c>) bound to <see cref="ReverseProxyOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "ReverseProxy";

    /// <summary>
    /// Gets or sets the individual proxy IP addresses whose forwarded headers are trusted (for example
    /// <c>["10.0.0.4"]</c>). Entries that are not a valid IP address are ignored. Empty by default, which — together
    /// with an empty <see cref="KnownNetworks"/> — disables forwarded-header processing entirely.
    /// </summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>
    /// Gets or sets the proxy networks, in CIDR notation, whose forwarded headers are trusted (for example
    /// <c>["172.16.0.0/12"]</c> for a container bridge network, where the proxy's address is assigned dynamically and
    /// cannot be pinned in <see cref="KnownProxies"/>). Entries that are not valid CIDR notation are ignored.
    /// </summary>
    /// <remarks>
    /// Keep this as tight as the deployment allows. A range such as <c>0.0.0.0/0</c> trusts every peer and reintroduces
    /// exactly the spoofing the allow-list exists to prevent.
    /// </remarks>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// Gets or sets how many proxy hops are consulted when reading a forwarded header. Defaults to <c>1</c>, matching a
    /// single reverse proxy in front of the application.
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
