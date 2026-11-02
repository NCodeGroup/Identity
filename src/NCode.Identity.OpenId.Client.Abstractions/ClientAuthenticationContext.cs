// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
