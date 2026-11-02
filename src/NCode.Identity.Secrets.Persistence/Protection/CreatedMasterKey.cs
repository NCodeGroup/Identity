// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Contains a newly created master key in both of its forms: the row that is persisted, which holds the key only in
/// a wrapped form, and the unwrapped key that protects new values.
/// </summary>
/// <param name="Row">The row to persist, which holds the master key only in a wrapped form.</param>
/// <param name="Key">The active master key, which the caller owns and must dispose.</param>
// See ADR-1235.
internal readonly record struct CreatedMasterKey(PersistedMasterKey Row, MasterKey Key);
