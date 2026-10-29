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

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Represents the failure to obtain an access token, with the HTTP status code and reason to answer with.
/// </summary>
[PublicAPI]
public sealed class BffTokenUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffTokenUnavailableException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to answer with.</param>
    /// <param name="reason">The reason that no token is available.</param>
    public BffTokenUnavailableException(int statusCode, string reason)
        : base(reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        StatusCode = statusCode;
        Reason = reason;
    }

    /// <summary>
    /// Gets the HTTP status code to answer with.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the reason that no token is available.
    /// </summary>
    public string Reason { get; }
}
