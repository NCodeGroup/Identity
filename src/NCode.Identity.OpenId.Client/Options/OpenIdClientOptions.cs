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

namespace NCode.Identity.OpenId.Client.Options;

/// <summary>
/// Configures how the OpenID client retrieves, caches and refreshes the metadata and signing keys of an OpenID Provider.
/// </summary>
[PublicAPI]
public sealed class OpenIdClientOptions
{
    /// <summary>
    /// Gets or sets how long retrieved provider metadata and signing keys are served before they are refreshed.
    /// The default is one hour.
    /// </summary>
    public TimeSpan MetadataRefreshInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the minimum time between two refreshes of the same provider, which bounds the traffic that an
    /// unknown signing key can cause. The default is five minutes.
    /// </summary>
    public TimeSpan MinimumRefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long previously retrieved metadata and signing keys may keep being used when a refresh fails.
    /// The default is 24 hours.
    /// </summary>
    public TimeSpan StaleLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the timeout of a single HTTP request to an OpenID Provider. The default is 15 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the maximum size, in bytes, of an HTTP response body that is read from an OpenID Provider.
    /// The default is 524288 (512 KiB).
    /// </summary>
    public int MaxResponseSizeBytes { get; set; } = 524288;

    /// <summary>
    /// Gets or sets the signing algorithms that the client prefers for a <c>private_key_jwt</c> client assertion, most
    /// preferred first, as JOSE <c>alg</c> names. When empty (the default), the client prefers <c>PS256, ES256, PS384,
    /// ES384, PS512, ES512, RS256, RS384, RS512</c>. The list is filtered to what the OpenID Provider advertises in
    /// <c>token_endpoint_auth_signing_alg_values_supported</c>, and an algorithm is only used when the key ring of the
    /// client holds a compatible asymmetric key.
    /// </summary>
    public List<string> ClientAssertionSigningAlgorithms { get; set; } = [];

    /// <summary>
    /// Gets or sets the JOSE <c>alg</c> names that an ID token may be signed with. When empty (the default), any
    /// registered asymmetric signature algorithm that the OpenID Provider advertises in
    /// <c>id_token_signing_alg_values_supported</c> is accepted. <c>none</c> and symmetric algorithms are never
    /// accepted.
    /// </summary>
    public List<string> IdTokenSigningAlgorithms { get; set; } = [];
}
