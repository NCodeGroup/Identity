// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Tokens.Models;

/// <summary>
/// The policy an access token is issued under, as <see cref="IAccessTokenPolicyService"/> evaluates it: the settings
/// that govern the token and the credentials it is signed and, when required, encrypted with.
/// </summary>
// See ADR-1158.
[PublicAPI]
public sealed class AccessTokenPolicy
{
    /// <summary>
    /// Gets the settings that govern the access token: the client's effective settings combined, by their lattice
    /// merges, with the settings of every resource server the token targets. Every access-token setting is read from
    /// this collection.
    /// </summary>
    public required IReadOnlySettingCollection Settings { get; init; }

    /// <summary>
    /// Gets the credentials the access token is signed with: always a tenant key.
    /// </summary>
    public required JoseSigningCredentials SigningCredentials { get; init; }

    /// <summary>
    /// Gets the credentials the access token is encrypted with, or <see langword="null"/> when its settings do not require
    /// encryption.
    /// </summary>
    public JoseEncryptionCredentials? EncryptionCredentials { get; init; }
}
