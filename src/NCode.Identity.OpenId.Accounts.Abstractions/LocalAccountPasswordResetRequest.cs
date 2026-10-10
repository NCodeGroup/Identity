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

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Describes a request to reset the credential of an existing local account: the target account and the replacement
/// password (carried as caller-owned bytes so it never lingers as a managed <see cref="string"/>). Resetting the
/// credential rotates the account's security stamp so that outstanding sessions and tokens are invalidated.
/// </summary>
[PublicAPI]
public sealed class LocalAccountPasswordResetRequest
{
    /// <summary>
    /// Gets the identifier of the local account whose credential is reset (its self-issued subject).
    /// </summary>
    public required string LocalAccountId { get; init; }

    /// <summary>
    /// Gets the UTF-8 bytes of the account's new password, carried in a caller-owned buffer so the sensitive material
    /// can be zeroed after use and never lingers as a managed <see cref="string"/>.
    /// </summary>
    public required ReadOnlyMemory<byte> Password { get; init; }
}
