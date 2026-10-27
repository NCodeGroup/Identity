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

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Carries the state a <see cref="IClientAuthenticator"/> needs to authenticate one request to the provider.
/// </summary>
/// <param name="registration">The client making the request.</param>
/// <param name="metadata">The metadata of the provider.</param>
/// <param name="endpoint">The URL of the endpoint the request is sent to.</param>
/// <param name="request">The HTTP request, whose headers an authenticator may set.</param>
/// <param name="parameters">The form parameters of the request, which an authenticator may append to.</param>
[PublicAPI]
public sealed class ClientAuthenticationContext(
    ClientRegistration registration,
    OpenIdProviderMetadata metadata,
    string endpoint,
    HttpRequestMessage request,
    ICollection<KeyValuePair<string, string>> parameters
)
{
    /// <summary>
    /// Gets the client making the request.
    /// </summary>
    public ClientRegistration Registration { get; } = registration;

    /// <summary>
    /// Gets the metadata of the provider.
    /// </summary>
    public OpenIdProviderMetadata Metadata { get; } = metadata;

    /// <summary>
    /// Gets the URL of the endpoint the request is sent to.
    /// </summary>
    public string Endpoint { get; } = endpoint;

    /// <summary>
    /// Gets the HTTP request, whose headers an authenticator may set.
    /// </summary>
    public HttpRequestMessage Request { get; } = request;

    /// <summary>
    /// Gets the form parameters of the request, which an authenticator may append to.
    /// </summary>
    public ICollection<KeyValuePair<string, string>> Parameters { get; } = parameters;
}
