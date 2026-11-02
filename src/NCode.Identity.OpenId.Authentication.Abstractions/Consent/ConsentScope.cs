// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Consent;

/// <summary>
/// Describes a single scope as presented on an interactive consent surface: its raw value, a friendly description (when
/// one is defined on the owning resource server), and whether the subject has already granted it.
/// </summary>
/// <param name="Value">The raw scope value (for example, <c>profile</c>).</param>
/// <param name="Description">A human-readable description of what the scope grants, or <see langword="null"/> when none
/// is defined.</param>
/// <param name="IsGranted">Whether the subject has already granted this scope (so the consent surface can pre-select
/// it).</param>
[PublicAPI]
public sealed record ConsentScope(string Value, string? Description, bool IsGranted);
