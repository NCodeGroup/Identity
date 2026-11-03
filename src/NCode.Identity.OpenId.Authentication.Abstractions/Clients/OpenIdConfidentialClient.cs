// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the configuration and details for an <c>OAuth</c> or <c>OpenID Connect</c> client that is confidential.
/// </summary>
[PublicAPI]
public abstract class OpenIdConfidentialClient : OpenIdClient
{
    /// <summary>
    /// Gets a <see cref="string"/> value containing the authentication method used to authenticate the client, which is
    /// always the method the client is registered to use.
    /// </summary>
    public abstract string AuthenticationMethod { get; }

    /// <summary>
    /// Gets the identifier of the registered credential the client authenticated with: the key identifier of the key
    /// or certificate it proved possession of, or of the shared secret it presented; or <c>null</c> when that
    /// credential has no identifier, such as a key without a <c>kid</c> published at the client's <c>jwks_uri</c>.
    /// </summary>
    public abstract string? CredentialId { get; }

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating whether the client was authenticated using a proof-of-possession key.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Confirmation))]
    public bool HasConfirmation => Confirmation != null;

    /// <summary>
    /// Gets the JSON payload containing the identifier for the proof-of-possession key used to authenticate the client.
    /// </summary>
    public abstract JsonElement? Confirmation { get; }
}
