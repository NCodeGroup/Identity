// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Subject;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;

/// <summary>
/// Represents the payload of a pending device authorization request (RFC 8628). The request is persisted keyed by its
/// <c>device_code</c>, transitions its <see cref="Status"/> out of band when the end user approves or denies it at the
/// verification endpoint, and is polled by the device at the token endpoint.
/// </summary>
[PublicAPI]
public readonly record struct DeviceCodeGrant
{
    /// <summary>
    /// Gets the identifier of the client that initiated the device authorization request.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the scopes requested by the client.
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>
    /// Gets the approval status of the request. See <see cref="DeviceAuthorizationStatus"/>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the authenticated subject that approved the request, or <c>null</c> while the request is pending or denied.
    /// </summary>
    public SubjectAuthentication? SubjectAuthentication { get; init; }

    /// <summary>
    /// Gets when the device most recently polled the token endpoint for this request, used to enforce the polling
    /// interval (the <c>slow_down</c> response).
    /// </summary>
    public DateTimeOffset? LastPolledWhen { get; init; }

    /// <summary>
    /// Gets any unrecognized members read from the at-rest JSON, captured so that members written by a newer schema
    /// version are preserved rather than silently dropped when this request is read and rewritten by an older node
    /// during a mixed-version deployment.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
