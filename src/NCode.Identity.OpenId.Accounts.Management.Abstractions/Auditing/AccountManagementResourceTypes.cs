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
