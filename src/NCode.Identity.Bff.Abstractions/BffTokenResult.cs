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

namespace NCode.Identity.Bff;

/// <summary>
/// Represents the outcome of resolving an access token: a token (or none), or a failure with an HTTP status code.
/// </summary>
[PublicAPI]
public sealed class BffTokenResult
{
    private BffTokenResult(BffAccessToken? token, int statusCode, string? reason)
    {
        Token = token;
        StatusCode = statusCode;
        Reason = reason;
    }

    /// <summary>
    /// Gets the access token when the request succeeded and a token applies; otherwise, <see langword="null"/>.
    /// </summary>
    public BffAccessToken? Token { get; }

    /// <summary>
    /// Gets the HTTP status code of the outcome, which is <c>200</c> on success.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the reason the request failed, or <see langword="null"/> on success.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Gets a value indicating whether the request succeeded.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Reason))]
    public bool IsSuccess => Reason is null;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="token">The access token, or <see langword="null"/> when no token applies.</param>
    /// <returns>The result.</returns>
    public static BffTokenResult Success(BffAccessToken? token) => new(token, 200, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to answer with.</param>
    /// <param name="reason">The reason the request failed, such as one of <see cref="BffTokenFailureReasons"/>.</param>
    /// <returns>The result.</returns>
    public static BffTokenResult Failure(int statusCode, string reason)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(statusCode, 400);
        ArgumentException.ThrowIfNullOrEmpty(reason);
        return new BffTokenResult(null, statusCode, reason);
    }
}
