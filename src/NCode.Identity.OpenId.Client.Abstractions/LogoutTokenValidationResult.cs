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
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents the outcome of validating a logout token: either its verified payload or the reason it is invalid.
/// </summary>
[PublicAPI]
public sealed class LogoutTokenValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutTokenValidationResult"/> class for a valid logout token.
    /// </summary>
    /// <param name="payload">The verified payload of the logout token.</param>
    /// <param name="issuer">The verified <c>iss</c> claim of the logout token.</param>
    /// <param name="tokenId">The <c>jti</c> claim of the logout token.</param>
    /// <param name="expiresAt">The expiry (the <c>exp</c> claim) of the logout token.</param>
    /// <param name="subject">The <c>sub</c> claim of the logout token, if any.</param>
    /// <param name="sessionId">The <c>sid</c> claim of the logout token, if any.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="issuer"/> or <paramref name="tokenId"/> is null or empty, or both <paramref name="subject"/> and <paramref name="sessionId"/>
    /// are null or empty.
    /// </exception>
    public LogoutTokenValidationResult(
        JsonElement payload,
        string issuer,
        string tokenId,
        DateTimeOffset expiresAt,
        string? subject,
        string? sessionId
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(issuer);
        ArgumentException.ThrowIfNullOrEmpty(tokenId);
        if (string.IsNullOrEmpty(subject) && string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("A logout token identifies a subject, a session, or both.");

        Payload = payload;
        Issuer = issuer;
        TokenId = tokenId;
        ExpiresAt = expiresAt;
        Subject = subject;
        SessionId = sessionId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutTokenValidationResult"/> class for an invalid logout token.
    /// </summary>
    /// <param name="failureCode">The machine-readable code of the reason the logout token is invalid.</param>
    /// <param name="failureReason">The reason the logout token is invalid.</param>
    public LogoutTokenValidationResult(string failureCode, string failureReason)
    {
        ArgumentException.ThrowIfNullOrEmpty(failureCode);
        ArgumentException.ThrowIfNullOrEmpty(failureReason);
        FailureCode = failureCode;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets the verified payload when the logout token is valid; otherwise, <see langword="default"/>.
    /// </summary>
    public JsonElement Payload { get; }

    /// <summary>
    /// Gets the verified <c>iss</c> claim when the logout token is valid; otherwise, <see langword="null"/>.
    /// </summary>
    public string? Issuer { get; }

    /// <summary>
    /// Gets the <c>jti</c> claim when the logout token is valid; otherwise, <see langword="null"/>.
    /// </summary>
    public string? TokenId { get; }

    /// <summary>
    /// Gets the expiry (the <c>exp</c> claim) of the logout token when it is valid; otherwise,
    /// <see langword="default"/>. The validator has already recorded the <c>jti</c> until this instant plus five
    /// minutes of clock skew.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets the <c>sub</c> claim when the logout token carries one; otherwise, <see langword="null"/>.
    /// </summary>
    public string? Subject { get; }

    /// <summary>
    /// Gets the <c>sid</c> claim when the logout token carries one; otherwise, <see langword="null"/>.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// Gets the machine-readable code of the reason the logout token is invalid, a
    /// <see cref="TokenValidationFailureCodes"/> value or another code a validator reports; otherwise,
    /// <see langword="null"/>.
    /// </summary>
    public string? FailureCode { get; }

    /// <summary>
    /// Gets the reason the logout token is invalid; otherwise, <see langword="null"/>.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// Gets a value indicating whether the logout token is valid.
    /// </summary>
    [MemberNotNullWhen(false, nameof(FailureCode), nameof(FailureReason))]
    [MemberNotNullWhen(true, nameof(TokenId), nameof(Issuer))]
    public bool IsValid => FailureCode is null;
}
