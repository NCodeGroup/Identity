// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Time;

namespace NCode.Identity.OpenId.Authentication.Tokens.Models;

/// <summary>
/// Represents a security token, which contains the token type, token value, and token lifetime.
/// </summary>
[PublicAPI]
public readonly record struct SecurityToken
{
    /// <summary>
    /// Gets the type of the token.
    /// </summary>
    public string TokenType { get; init; }

    /// <summary>
    /// Gets the value of the token.
    /// </summary>
    public string TokenValue { get; init; }

    /// <summary>
    /// Gets the lifetime of the token.
    /// </summary>
    public TimePeriod TokenLifetime { get; init; }
}
