// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.OutboundHttp;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Contains the names of the causes that stop a client's <c>jwks_uri</c> fetch from yielding a key, as logged by
/// <see cref="DefaultClientKeyResolver"/>.
/// </summary>
/// <remarks>
/// The reasons are a closed vocabulary: each is one of the resolver's own failure sites, no other package
/// can add a site to the resolver, and a new reason would need a change to the resolver anyway. Names compare
/// ordinally.
/// </remarks>
// See ADR-1106 and ADR-1150.
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
    public const string BlockedAddress = OutboundFetchFailureReasons.BlockedAddress;

    /// <summary>
    /// The <c>jwks_uri</c> host name did not resolve.
    /// </summary>
    public const string NameResolution = OutboundFetchFailureReasons.NameResolution;

    /// <summary>
    /// The TLS handshake with the <c>jwks_uri</c> host failed.
    /// </summary>
    public const string Tls = OutboundFetchFailureReasons.Tls;

    /// <summary>
    /// The fetch did not complete within <c>jwks_uri_fetch_timeout</c>.
    /// </summary>
    public const string Timeout = OutboundFetchFailureReasons.Timeout;

    /// <summary>
    /// The fetch was canceled.
    /// </summary>
    public const string Canceled = OutboundFetchFailureReasons.Canceled;

    /// <summary>
    /// The connection to the <c>jwks_uri</c> host failed for a cause that no other reason names.
    /// </summary>
    public const string Connection = OutboundFetchFailureReasons.Connection;

    /// <summary>
    /// The <c>jwks_uri</c> answered with a status code that is not a success.
    /// </summary>
    public const string HttpStatus = OutboundFetchFailureReasons.HttpStatus;

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
