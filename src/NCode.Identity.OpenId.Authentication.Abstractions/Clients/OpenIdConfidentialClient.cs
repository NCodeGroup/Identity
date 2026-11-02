// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

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
    /// Gets the <see cref="SecretKey"/> that was used to authenticate the client.
    /// </summary>
    public abstract SecretKey SecretKey { get; }

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
