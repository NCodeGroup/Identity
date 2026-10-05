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
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when an <c>OAuth</c> or <c>OpenID Connect</c> protocol endpoint returns an
/// error response, whether the error was thrown or returned directly. The event is published once per
/// errored request from the single endpoint-filter funnel, so no publishing site is required at the
/// individual error call sites.
/// </summary>
[PublicAPI]
public sealed record OpenIdErrorEvent : AuditEvent
{
    /// <inheritdoc />
    public override string Action => OpenIdAuditActions.Error;

    /// <summary>
    /// Gets the <c>OAuth</c> or <c>OpenID Connect</c> error code (the <c>error</c> parameter).
    /// </summary>
    public required string ErrorCode { get; init; }

    /// <summary>
    /// Gets the non-sensitive, human-readable error description (the <c>error_description</c> parameter),
    /// if any.
    /// </summary>
    public string? ErrorDescription { get; init; }

    /// <summary>
    /// Gets the HTTP status code used for the error response, if known.
    /// </summary>
    public int? StatusCode { get; init; }
}
