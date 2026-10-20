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
/// Represents the REST resource that previews a tenant's address — its identifier and issuer — before the tenant is
/// created or its issuer is changed, judged by the same rules as the write itself.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantAddressResource
{
    /// <summary>
    /// Gets the identifier of the tenant.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the issuer the tenant has or would have: the proposed issuer, else the tenant's current issuer, else the one
    /// its tenant strategy derives for it; or <c>null</c> when there is none.
    /// </summary>
    public required string? Issuer { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Issuer"/> is the one the tenant strategy derives, which a tenant created
    /// or updated without an issuer is given.
    /// </summary>
    public required bool IsIssuerDerived { get; init; }

    /// <summary>
    /// Gets a value indicating whether a tenant with the identifier already exists, so a new tenant cannot take it.
    /// </summary>
    public required bool Exists { get; init; }

    /// <summary>
    /// Gets the reasons the tenant cannot be served at the address; empty when it can.
    /// </summary>
    public required IReadOnlyList<TenantAddressFailure> Failures { get; init; }
}
