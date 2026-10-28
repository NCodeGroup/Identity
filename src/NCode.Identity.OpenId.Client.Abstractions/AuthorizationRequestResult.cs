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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents the outcome of creating an authorization request: either the <see cref="Uri"/> to send the user agent to
/// or a <see cref="ProtocolError"/> from the pushed authorization request endpoint.
/// </summary>
[PublicAPI]
public sealed class AuthorizationRequestResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationRequestResult"/> class for a front-channel request.
    /// </summary>
    /// <param name="authorizationUri">The authorization endpoint URL carrying the request parameters.</param>
    public AuthorizationRequestResult(Uri authorizationUri)
    {
        ArgumentNullException.ThrowIfNull(authorizationUri);
        AuthorizationUri = authorizationUri;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationRequestResult"/> class for a pushed request (RFC 9126).
    /// </summary>
    /// <param name="authorizationUri">The authorization endpoint URL carrying the <c>request_uri</c>.</param>
    /// <param name="requestUri">The <c>request_uri</c> returned by the pushed authorization request endpoint.</param>
    /// <param name="expiresIn">The lifetime of the <paramref name="requestUri"/> in seconds.</param>
    public AuthorizationRequestResult(Uri authorizationUri, string requestUri, int expiresIn)
    {
        ArgumentNullException.ThrowIfNull(authorizationUri);
        ArgumentException.ThrowIfNullOrEmpty(requestUri);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expiresIn);
        AuthorizationUri = authorizationUri;
        RequestUri = requestUri;
        ExpiresIn = expiresIn;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationRequestResult"/> class for an error response.
    /// </summary>
    /// <param name="error">The protocol error.</param>
    public AuthorizationRequestResult(ProtocolError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    /// <summary>
    /// Gets the URL to send the user agent to when the request succeeded; otherwise, <see langword="null"/>.
    /// </summary>
    public Uri? AuthorizationUri { get; }

    /// <summary>
    /// Gets the <c>request_uri</c> when the request was pushed; otherwise, <see langword="null"/>.
    /// </summary>
    public string? RequestUri { get; }

    /// <summary>
    /// Gets the lifetime of the <see cref="RequestUri"/> in seconds when the request was pushed; otherwise,
    /// <see langword="null"/>.
    /// </summary>
    public int? ExpiresIn { get; }

    /// <summary>
    /// Gets the protocol error when the request failed; otherwise, <see langword="null"/>.
    /// </summary>
    public ProtocolError? Error { get; }

    /// <summary>
    /// Gets a value indicating whether the request was pushed to the provider.
    /// </summary>
    public bool IsPushed => RequestUri is not null;

    /// <summary>
    /// Gets a value indicating whether the request succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(AuthorizationUri))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => AuthorizationUri is not null;
}
