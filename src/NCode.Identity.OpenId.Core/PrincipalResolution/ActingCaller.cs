// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Identifies the caller that may be a client acting for itself: the tenant whose client it names, its subject and the
/// client its <c>client_id</c> claim names.
/// </summary>
/// <param name="TenantId">The identifier of the tenant whose client the caller names.</param>
/// <param name="Subject">The subject of the caller, derived through the environment's <c>GetSubjectId</c>.</param>
/// <param name="ClientId">The identifier of the client that the caller's <c>client_id</c> claim names.</param>
// See ADR-1466.
internal readonly record struct ActingCaller(string TenantId, string Subject, string ClientId);
