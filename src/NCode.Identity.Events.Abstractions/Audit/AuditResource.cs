// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Describes an entity that an audited operation acted on.
/// </summary>
/// <param name="Type">
/// The kind of entity, typically one of the <see cref="AuditResourceTypes"/> codes or a code that a
/// sibling package declares for its own entities.
/// </param>
/// <param name="Id">
/// The identifier of the entity within its <paramref name="Type"/>, or <see langword="null"/> when
/// the entity has no identifier (for example it was not created).
/// </param>
[PublicAPI]
public sealed record AuditResource(string Type, string? Id);
