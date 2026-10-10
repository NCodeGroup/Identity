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
/// Contains constants for local accounts.
/// </summary>
[PublicAPI]
public static class AccountConstants
{
    /// <summary>
    /// The stable, global issuer value for a self-issued local-account connection. Local accounts are global (one per
    /// human, like a federated principal), so the issuer is a fixed constant rather than a per-tenant value — the same
    /// account therefore resolves to the same principal regardless of which tenant it signs in through.
    /// </summary>
    public const string SelfIssuer = "urn:ncode:identity:local-account";

    /// <summary>
    /// The claim type under which an account's owner-updatable <c>ProfileMetadata</c> bag is projected — as a single
    /// JSON object claim — into issued tokens and the UserInfo response.
    /// </summary>
    public const string ProfileMetadataClaimType = "profile_metadata";

    /// <summary>
    /// The claim type under which an account's server-controlled <c>SystemMetadata</c> bag is projected — as a single
    /// JSON object claim — into issued tokens and the UserInfo response. This bag may carry authorization-relevant data
    /// and is never end-user-writable.
    /// </summary>
    public const string SystemMetadataClaimType = "system_metadata";
}
