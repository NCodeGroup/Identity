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

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents an error response returned by an OpenID provider.
/// </summary>
/// <param name="error">The error code.</param>
/// <param name="statusCode">The HTTP status code of the response.</param>
[PublicAPI]
public sealed class ProtocolError(string error, System.Net.HttpStatusCode statusCode)
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string Error { get; } = error;

    /// <summary>
    /// Gets the HTTP status code of the response.
    /// </summary>
    public System.Net.HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>
    /// Gets the human-readable error description, if any.
    /// </summary>
    public string? ErrorDescription { get; init; }

    /// <summary>
    /// Gets the URI of a page with more information about the error, if any.
    /// </summary>
    public string? ErrorUri { get; init; }
}
