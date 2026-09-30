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

namespace NCode.Identity.OpenId.Management.Contracts.Grants;

/// <summary>
/// Represents the result of a bulk grant revocation, reporting how many grants were revoked.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class GrantRevocationResult
{
    /// <summary>
    /// Gets the number of grants that were revoked by the operation. Already-revoked grants are not counted.
    /// </summary>
    public required long Revoked { get; init; }
}
