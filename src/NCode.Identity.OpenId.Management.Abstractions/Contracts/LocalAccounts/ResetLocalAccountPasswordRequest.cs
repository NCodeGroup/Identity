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

namespace NCode.Identity.OpenId.Management.Contracts.LocalAccounts;

/// <summary>
/// Represents the request body to reset a local account's credential. Resetting the credential rotates the account's
/// security stamp, invalidating its outstanding sessions and tokens.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ResetLocalAccountPasswordRequest
{
    /// <summary>
    /// Gets the account's new password, hashed server-side.
    /// </summary>
    public required string Password { get; init; }
}
