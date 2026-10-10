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
}
