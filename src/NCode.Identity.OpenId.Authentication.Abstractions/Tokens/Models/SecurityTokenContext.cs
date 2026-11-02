// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.OpenId.Authentication.Tokens.Models;

/// <summary>
/// Contains contextual information about a security token.
/// </summary>
[PublicAPI]
public readonly record struct SecurityTokenContext(
    CreateSecurityTokenRequest TokenRequest,
    JoseSigningCredentials SigningCredentials,
    JoseEncryptionCredentials? EncryptionCredentials,
    string TokenType
);
