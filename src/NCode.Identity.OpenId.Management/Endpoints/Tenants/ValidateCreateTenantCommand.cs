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
using Microsoft.AspNetCore.Http;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/// <summary>
/// Represents a mediator command that validates the preconditions for creating an OpenID Tenant. Handlers run
/// in priority order and populate <see cref="Disposition"/> on the first failing precondition. See
/// <see href="../../../docs/adr/0013-management-precondition-mediator-pipeline.md">ADR-0013</see>.
/// </summary>
/// <param name="HttpContext">The <see cref="HttpContext"/> for the current request.</param>
/// <param name="Tenant">The candidate <see cref="PersistedTenant"/> to be created.</param>
/// <param name="Disposition">The shared disposition that handlers populate with the first precondition failure.</param>
[PublicAPI]
public readonly record struct ValidateCreateTenantCommand(
    HttpContext HttpContext,
    PersistedTenant Tenant,
    OperationDisposition<ManagementError> Disposition
) : ICommand;
