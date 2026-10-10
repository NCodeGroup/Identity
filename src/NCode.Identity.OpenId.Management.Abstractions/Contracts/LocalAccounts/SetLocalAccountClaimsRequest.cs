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
/// Represents the request body to replace a local account's full set of profile claims. The supplied claims replace
/// the current set in its entirety.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SetLocalAccountClaimsRequest
{
    /// <summary>
    /// Gets the replacement profile claims.
    /// </summary>
    public required IReadOnlyList<LocalAccountClaim> Claims { get; init; }
}
