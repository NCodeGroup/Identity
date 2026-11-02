// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Serialization;

/// <summary>
/// Contains the properties for serializing and deserializing <see cref="IAuthorizationRequest"/> instances
/// to and from JSON.
/// </summary>
public readonly record struct AuthorizationRequestJsonEnvelope
{
    /// <summary>
    /// Gets a value indicating whether the request is a continuation.
    /// </summary>
    public required bool IsContinuation { get; init; }

    /// <summary>
    /// Gets the consent-narrowed subset of the requested scopes, or <see langword="null"/> when no consent
    /// narrowing has been applied.
    /// </summary>
    public IReadOnlyList<string>? GrantedScopes { get; init; }

    /// <summary>
    /// Gets the original request message.
    /// </summary>
    public required IAuthorizationRequestMessage? OriginalRequestMessage { get; init; }

    /// <summary>
    /// Gets the original request object.
    /// </summary>
    public required IAuthorizationRequestObject? OriginalRequestObject { get; init; }
}
