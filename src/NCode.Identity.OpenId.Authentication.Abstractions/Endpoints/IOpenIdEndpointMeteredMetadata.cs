#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Represents whether a request to the associated endpoint is the tenant's metered usage: counted against the tenant's
/// daily request quota and its overage, and recorded in the tenant's usage ledger. An endpoint that serves public,
/// unauthenticated metadata — such as the discovery document or the JSON Web Key Set — is not metered, so anonymous
/// traffic cannot spend a tenant's quota or bill its overage; the per-minute rate limits still apply to it. An endpoint
/// without this metadata is metered.
/// </summary>
// See ADR-0198 and ADR-0199.
[PublicAPI]
public interface IOpenIdEndpointMeteredMetadata
{
    /// <summary>
    /// Gets a value indicating whether requests to the associated endpoint are metered.
    /// </summary>
    bool IsMetered { get; }
}
