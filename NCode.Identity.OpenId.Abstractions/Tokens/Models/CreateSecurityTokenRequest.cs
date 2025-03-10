﻿#region Copyright Preamble

// Copyright @ 2024 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Tokens.Models;

/// <summary>
/// Contains the information required to create a security token.
/// </summary>
[PublicAPI]
public readonly record struct CreateSecurityTokenRequest
{
    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when the token was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets or sets the type of grant that is being used to generate the token.
    /// </summary>
    public required string GrantType { get; init; }

    /// <summary>
    /// Gets or sets the <c>nonce</c> value that was provided by the client application.
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>
    /// Gets or sets the <c>state</c> value that was provided by the client application.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets or sets the original scopes that were requested by the client application.
    /// </summary>
    public required IReadOnlyList<string> OriginalScopes { get; init; }

    /// <summary>
    /// Gets or sets the effective scopes that will be used to generate the token.
    /// </summary>
    public required IReadOnlyList<string> EffectiveScopes { get; init; }

    /// <summary>
    /// Gets or sets the <c>code</c> value that was provided by the client application.
    /// </summary>
    public string? AuthorizationCode { get; init; }

    /// <summary>
    /// Gets or sets the <c>access_token</c> value that was generated by the authorization server.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Gets or sets the <c>refresh_token</c> value that was generated by the authorization server.
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="SubjectAuthentication"/> that contains the information about the authenticated end-user.
    /// </summary>
    public SubjectAuthentication? SubjectAuthentication { get; init; }

    /// <summary>
    /// Gets or sets additional claims that will be included in the generated token.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraPayloadClaims { get; init; }

    /// <summary>
    /// Gets or sets additional claims that will be included in the signature header.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraSignatureHeaderClaims { get; init; }

    /// <summary>
    /// Gets or sets additional claims that will be included in the encryption header.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraEncryptionHeaderClaims { get; init; }
}
