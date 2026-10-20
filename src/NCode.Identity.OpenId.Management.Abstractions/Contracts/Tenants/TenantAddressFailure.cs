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

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents one reason a tenant cannot be served at a proposed address, as reported by
/// <see cref="TenantAddressResource.Failures"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantAddressFailure
{
    /// <summary>
    /// Gets a message describing the failure.
    /// </summary>
    public required string Detail { get; init; }

    /// <summary>
    /// Gets a value indicating whether the failure is a conflict with another tenant — an address another tenant already
    /// holds, which a write answers with <c>409</c> — rather than a value that is invalid on its own (<c>400</c>).
    /// </summary>
    public required bool IsConflict { get; init; }
}
