// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Authentication.Messages.Parsers;

/// <summary>
/// Contains constants for various <see cref="IParameterParser{T}"/> implementations that are used by <c>OAuth</c>
/// and <c>OpenID Connect</c> messages.
/// </summary>
[PublicAPI]
public static class AuthenticationParameterParsers
{
    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="IRequestClaims"/> from a JSON payload.
    /// </summary>
    public static JsonParser<IRequestClaims> RequestClaims { get; } = new();
}
