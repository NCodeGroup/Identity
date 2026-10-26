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

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Contains a payload that was read from a persisted grant, so that a missing payload (<c>null</c>) is distinct from a
/// payload whose value is <c>null</c>.
/// </summary>
/// <param name="Value">The payload.</param>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
// See ADR-0265.
internal sealed record GrantPayload<TPayload>(TPayload Value);

/// <summary>
/// Protects the payload of a persisted grant under the master key of its tenant, on top of the schema-versioned
/// serialization of <see cref="IPersistedPayloadSerializer"/>.
/// </summary>
// See ADR-0265.
internal interface IGrantPayloadProtector
{
    /// <summary>
    /// Serializes and protects the payload of a grant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that owns the grant.</param>
    /// <param name="hashedKey">The hashed key of the grant, which binds the protected payload to its lookup row.</param>
    /// <param name="payload">The payload to protect.</param>
    /// <param name="jsonOptions">The options to serialize the payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <typeparam name="TPayload">The type of the payload.</typeparam>
    /// <returns>The protected payload, which is not secret.</returns>
    ValueTask<string> ProtectAsync<TPayload>(
        string tenantId,
        string hashedKey,
        TPayload payload,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Unprotects and deserializes the payload of a grant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that owns the grant.</param>
    /// <param name="grantType">The type of the grant, for diagnostics.</param>
    /// <param name="hashedKey">The hashed key of the grant.</param>
    /// <param name="protectedPayload">The protected payload.</param>
    /// <param name="jsonOptions">The options to deserialize the payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <typeparam name="TPayload">The type of the payload.</typeparam>
    /// <returns>The payload, or <c>null</c> when it could not be unprotected.</returns>
    ValueTask<GrantPayload<TPayload>?> UnprotectOrDefaultAsync<TPayload>(
        string tenantId,
        string grantType,
        string hashedKey,
        string protectedPayload,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken
    );
}
