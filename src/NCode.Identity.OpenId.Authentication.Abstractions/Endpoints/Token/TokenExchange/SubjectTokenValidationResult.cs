#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;

/// <summary>
/// Contains the result of validating a <c>subject_token</c> or <c>actor_token</c> presented to an OAuth 2.0 Token
/// Exchange (<see href="https://datatracker.ietf.org/doc/html/rfc8693">RFC 8693</see>).
/// </summary>
[PublicAPI]
public sealed class SubjectTokenValidationResult
{
    /// <summary>
    /// Creates a <see cref="SubjectTokenValidationResult"/> that represents a failed validation.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> describing why validation failed.</param>
    /// <returns>The failed <see cref="SubjectTokenValidationResult"/>.</returns>
    public static SubjectTokenValidationResult Fail(IOpenIdError error) => new() { Error = error };

    /// <summary>
    /// Creates a <see cref="SubjectTokenValidationResult"/> that represents a successful validation.
    /// </summary>
    /// <param name="principal">The validated <see cref="ClaimsPrincipal"/>.</param>
    /// <param name="payload">The decoded token payload, used to read structured claims (such as <c>aud</c>,
    /// <c>client_id</c>, <c>act</c>, and <c>may_act</c>).</param>
    /// <param name="tokenTypeIdentifier">The RFC 8693 token type identifier the token was validated as.</param>
    /// <returns>The successful <see cref="SubjectTokenValidationResult"/>.</returns>
    public static SubjectTokenValidationResult Success(
        ClaimsPrincipal principal,
        JsonElement payload,
        string tokenTypeIdentifier
    ) =>
        new()
        {
            Principal = principal,
            Payload = payload,
            TokenTypeIdentifier = tokenTypeIdentifier,
        };

    /// <summary>
    /// Gets a value indicating whether the token was successfully validated.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    [MemberNotNullWhen(true, nameof(Principal))]
    public bool IsValid => Error is null;

    /// <summary>
    /// Gets the <see cref="IOpenIdError"/> describing why validation failed, or <see langword="null"/> on success.
    /// </summary>
    public IOpenIdError? Error { get; init; }

    /// <summary>
    /// Gets the validated <see cref="ClaimsPrincipal"/>, or <see langword="null"/> on failure.
    /// </summary>
    public ClaimsPrincipal? Principal { get; init; }

    /// <summary>
    /// Gets the decoded token payload, used to read structured claims that are awkward to read from the flattened
    /// <see cref="Principal"/> (such as the <c>act</c> and <c>may_act</c> JSON objects).
    /// </summary>
    public JsonElement Payload { get; init; }

    /// <summary>
    /// Gets the RFC 8693 token type identifier the token was validated as.
    /// </summary>
    public string TokenTypeIdentifier { get; init; } = string.Empty;
}
