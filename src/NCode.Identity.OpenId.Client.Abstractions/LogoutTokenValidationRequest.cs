// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Describes a logout token to validate.
/// </summary>
[PublicAPI]
public sealed class LogoutTokenValidationRequest
{
    /// <summary>
    /// Gets the compact-serialized <c>logout_token</c> posted to the back-channel logout endpoint.
    /// </summary>
    public required string LogoutToken { get; init; }
}
