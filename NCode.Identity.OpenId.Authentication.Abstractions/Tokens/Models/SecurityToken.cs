#region Copyright Preamble

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
using NCode.Identity.Models;

namespace NCode.Identity.OpenId.Authentication.Tokens.Models;

/// <summary>
/// Represents a security token, which contains the token type, token value, and token lifetime.
/// </summary>
[PublicAPI]
public readonly record struct SecurityToken
{
    /// <summary>
    /// Gets or sets the type of the token.
    /// </summary>
    public string TokenType { get; init; }

    /// <summary>
    /// Gets or sets the value of the token.
    /// </summary>
    public string TokenValue { get; init; }

    /// <summary>
    /// Gets or sets the lifetime of the token.
    /// </summary>
    public TimePeriod TokenLifetime { get; init; }
}
