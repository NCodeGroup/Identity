// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Management.Auditing;

/// <summary>
/// Declares the well-known <c>ResourceType</c> values emitted by the local-account management audit events.
/// </summary>
[PublicAPI]
public static class AccountManagementResourceTypes
{
    /// <summary>
    /// A local account.
    /// </summary>
    public const string LocalAccount = "local_account";
}
