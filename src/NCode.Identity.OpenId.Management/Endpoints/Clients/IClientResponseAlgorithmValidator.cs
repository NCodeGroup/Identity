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

using System.Text.Json;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Checks a change to the settings above a tenant's clients against the algorithms each client registers for the JWTs
/// addressed to it (<see cref="ClientResponseAlgorithmRule"/>).
/// </summary>
// See ADR-0155.
internal interface IClientResponseAlgorithmValidator
{
    /// <summary>
    /// Merges each client of a tenant onto the tenant's effective settings before and after a change, as the runtime
    /// merges it, and returns the algorithms the change excludes. A mismatch that predates the change is not returned,
    /// so it never blocks a write the change does not affect.
    /// </summary>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to page through the tenant's clients.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant whose clients to check.</param>
    /// <param name="currentTenantSettings">The tenant's effective settings before the change.</param>
    /// <param name="newTenantSettings">The tenant's effective settings after the change.</param>
    /// <param name="jsonOptions">The <see cref="JsonSerializerOptions"/> used to read each client's settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ClientResponseAlgorithmFinding"/> instances, or an empty list.</returns>
    ValueTask<IReadOnlyList<ClientResponseAlgorithmFinding>> FindIntroducedViolationsAsync(
        IStoreManager storeManager,
        string tenantId,
        IReadOnlySettingCollection currentTenantSettings,
        IReadOnlySettingCollection newTenantSettings,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken
    );
}
