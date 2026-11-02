// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
