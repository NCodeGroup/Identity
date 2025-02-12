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
using NCode.Identity.OpenId.Models;

namespace NCode.Identity.OpenId.Tokens.Models;

/// <summary>
/// Represents a security token, which contains the token type, token value, and token period.
/// </summary>
[PublicAPI]
public readonly record struct SecurityToken(
    string TokenType,
    string TokenValue,
    TimePeriod TokenPeriod
);
