// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Contains the names of the causes that stop a client's <c>jwks_uri</c> fetch from yielding a key, as logged by
/// <see cref="DefaultClientKeyResolver"/>.
/// </summary>
/// <remarks>
/// The reasons are a closed vocabulary (ADR-1150): each is one of the resolver's own failure sites, no other package
/// can add a site to the resolver, and a new reason would need a change to the resolver anyway. Names compare
/// ordinally.
/// </remarks>
// See ADR-1106.
internal static class ClientJwksFetchFailureReasons
{
    /// <summary>
    /// The <c>jwks_uri</c> is not an absolute <c>https</c> URI.
    /// </summary>
    public const string NotHttps = "not_https";

    /// <summary>
    /// The <c>jwks_uri</c> host is not in <c>jwks_uri_allowed_hosts</c>.
    /// </summary>
    public const string HostNotAllowed = "host_not_allowed";

    /// <summary>
    /// The outbound guard refused every address the <c>jwks_uri</c> host resolves to.
    /// </summary>
    public const string BlockedAddress = "blocked_address";

    /// <summary>
    /// The <c>jwks_uri</c> host name did not resolve.
    /// </summary>
    public const string NameResolution = "name_resolution";

    /// <summary>
    /// The TLS handshake with the <c>jwks_uri</c> host failed.
    /// </summary>
    public const string Tls = "tls";

    /// <summary>
    /// The fetch did not complete within <c>jwks_uri_fetch_timeout</c>.
    /// </summary>
    public const string Timeout = "timeout";

    /// <summary>
    /// The fetch was canceled.
    /// </summary>
    public const string Canceled = "canceled";

    /// <summary>
    /// The connection to the <c>jwks_uri</c> host failed for a cause that no other reason names.
    /// </summary>
    public const string Connection = "connection";

    /// <summary>
    /// The <c>jwks_uri</c> answered with a status code that is not a success.
    /// </summary>
    public const string HttpStatus = "http_status";

    /// <summary>
    /// The <c>jwks_uri</c> document exceeds <c>jwks_uri_max_size_bytes</c>.
    /// </summary>
    public const string TooLarge = "too_large";

    /// <summary>
    /// The <c>jwks_uri</c> document is not JSON.
    /// </summary>
    public const string NotJson = "not_json";

    /// <summary>
    /// The key set at the <c>jwks_uri</c> was refused by the client-key policy.
    /// </summary>
    public const string InvalidKeySet = "invalid_key_set";
}
