#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Providers;

/// <summary>
/// Represents the state used to load the tenant's <see cref="SecretKey"/> collection.
/// </summary>
public class LoadSecretsState
{
    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the last known <see cref="PersistedSecret"/> collection.
    /// </summary>
    public required ConcurrentState<IReadOnlyCollection<PersistedSecret>> Secrets { get; set; }
}
