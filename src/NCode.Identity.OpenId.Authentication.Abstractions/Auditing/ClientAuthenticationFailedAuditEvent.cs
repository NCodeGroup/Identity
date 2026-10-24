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

using System.Diagnostics;
using JetBrains.Annotations;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when authenticating a client at the token endpoint fails.
/// </summary>
[PublicAPI]
public sealed record ClientAuthenticationFailedAuditEvent : AuditEvent, ISupportMetricAttributes
{
    /// <inheritdoc />
    public override string Action => OpenIdAuditActions.ClientAuthentication;

    /// <summary>
    /// The name of the metric attribute that carries <see cref="AuthenticationMethod"/> on the counter of published
    /// events.
    /// </summary>
    public const string AuthenticationMethodMetricAttribute =
        "ncode.identity.client_authentication.method";

    /// <summary>
    /// Gets the registered client-authentication method the client attempted, such as <c>private_key_jwt</c>.
    /// </summary>
    public required string AuthenticationMethod { get; init; }

    /// <inheritdoc />
    public void AddMetricAttributes(ref TagList tags) =>
        tags.Add(AuthenticationMethodMetricAttribute, AuthenticationMethod);
}
