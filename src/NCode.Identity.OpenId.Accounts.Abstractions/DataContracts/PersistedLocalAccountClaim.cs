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

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.DataContracts;

/// <summary>
/// Contains the data for a single profile claim owned by a <see cref="PersistedLocalAccount"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedLocalAccountClaim
{
    /// <summary>
    /// Gets or sets the claim type.
    /// </summary>
    [MaxLength(AccountMaxLengths.ClaimType)]
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the claim value.
    /// </summary>
    [MaxLength(AccountMaxLengths.ClaimValue)]
    public required string Value { get; init; }
}
