// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Provides default values for the OpenID protocol client.
/// </summary>
[PublicAPI]
public static class OpenIdClientDefaults
{
    /// <summary>
    /// The name of the <c>IHttpClientFactory</c> client that the protocol client sends its
    /// requests through.
    /// </summary>
    public const string HttpClientName = "NCode.Identity.OpenId.Client";

    /// <summary>
    /// The prefix of the names of the <c>IHttpClientFactory</c> clients that present a TLS client certificate. The rest
    /// of the name is the uppercase hexadecimal SHA-256 hash of the certificate's DER encoding.
    /// </summary>
    public const string MutualTlsHttpClientNamePrefix = "NCode.Identity.OpenId.Client.MutualTls:";
}
