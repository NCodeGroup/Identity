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
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Messages;

/// <summary>
/// Represents the message for an <c>OAuth 2.0</c> token introspection request
/// (<see href="https://datatracker.ietf.org/doc/html/rfc7662">RFC 7662</see>).
/// </summary>
[PublicAPI]
public interface ITokenIntrospectionRequest : IOpenIdRequest
{
    /// <summary>
    /// Gets or sets the token for which introspection metadata is requested.
    /// </summary>
    string? Token { get; set; }

    /// <summary>
    /// Gets or sets the optional hint about the type of the token being introspected (for example, <c>access_token</c>
    /// or <c>refresh_token</c>).
    /// </summary>
    string? TokenTypeHint { get; set; }
}
