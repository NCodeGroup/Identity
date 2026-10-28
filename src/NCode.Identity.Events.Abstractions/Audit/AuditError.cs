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

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Describes why an audited operation failed or was denied.
/// </summary>
/// <remarks>
/// An error never carries secret material: neither <see cref="Message"/> nor <see cref="Code"/> may
/// contain a credential, token, authorization code or other secret value.
/// </remarks>
/// <param name="Code">
/// A stable, machine-readable error code (for example an OAuth error code such as
/// <c>invalid_client</c>) that an operator can filter and pivot on.
/// </param>
/// <param name="Message">
/// Human-readable text that describes the error, or <see langword="null"/> when there is none.
/// </param>
/// <param name="StatusCode">
/// The HTTP status code that was returned to the caller, or <see langword="null"/> when the error
/// did not produce an HTTP response.
/// </param>
[PublicAPI]
public sealed record AuditError(string Code, string? Message = null, int? StatusCode = null);
