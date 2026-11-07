// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Contains the resolved registration of a client that <see cref="IOpenIdClientFactory"/> builds an
/// <see cref="OpenIdClient"/> from.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class OpenIdClientDefinition
{
    /// <summary>
    /// Gets the client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the identifier of the client's principal: the subject of a token the client obtains for itself.
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the client's effective settings: the tenant's settings merged with the client's own.
    /// </summary>
    public required IReadOnlySettingCollection Settings { get; init; }

    /// <summary>
    /// Gets the secrets only known to the client.
    /// </summary>
    public required IReadOnlyCollection<SecretKey> Secrets { get; init; }

    /// <summary>
    /// Gets the one-way verifiers of the shared secrets the client presents that the server only compares.
    /// </summary>
    public required IReadOnlyCollection<SecretVerifier> SecretVerifiers { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client acts for, or <c>null</c> for an ordinary client.
    /// </summary>
    public string? ResourceServerId { get; init; }
}
